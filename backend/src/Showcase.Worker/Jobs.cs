using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Showcase.Application;
using Showcase.Domain;
using Showcase.Infrastructure;

namespace Showcase.Worker;

/// <summary>
/// The processing pipeline for one uploaded file:
/// convert (.fbx → .glb with Blender; a .glb is used as is) → stats (SharpGLTF; also proves the file is
/// readable) → optimize (gltf-transform: Meshopt geometry, WebP textures, plus a lighter phone copy above
/// 150K triangles). Each step is saved on the job as it runs, so the Studio can show progress.
/// </summary>
public class ModelFileProcessor(
    AppDbContext db,
    IFileStorage storage,
    IOptions<PipelineOptions> options,
    ILogger<ModelFileProcessor> log) : IModelFileProcessor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly PipelineOptions _o = options.Value;
    private List<JobStepDto> _steps = [];
    private ProcessingJob _job = null!;

    public async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        _job = await db.ProcessingJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)
               ?? throw new InvalidOperationException($"Processing job {jobId} not found.");
        _steps = JsonSerializer.Deserialize<List<JobStepDto>>(_job.Steps, Json) ?? [];
        var file = await db.ModelFiles.FirstAsync(f => f.Id == _job.ModelFileId, ct);
        var work = Directory.CreateDirectory(Path.Combine(_o.WorkDir, jobId.ToString("N"))).FullName;
        var timeout = TimeSpan.FromMinutes(_o.TimeoutMinutes);

        _job.Status = JobStatus.Running;
        _job.StartedAt = DateTimeOffset.UtcNow;
        await SaveAsync(ct);
        var current = PipelineSteps.Convert;
        try
        {
            // 1. Convert
            var input = Path.Combine(work, "input." + file.Format);
            await using (var src = storage.OpenRead(file.StorageKey))
            await using (var dst = File.Create(input))
                await src.CopyToAsync(dst, ct);
            var glb = Path.Combine(work, "model.glb");
            if (file.Format == "fbx")
            {
                var message = await StepAsync(current, async () =>
                {
                    var script = Path.Combine(AppContext.BaseDirectory, "scripts", "fbx_to_glb.py");
                    var output = await RunBlenderAsync(script, input, glb, timeout, ct);
                    return output;
                }, ct);
                log.LogInformation("Converted {File}: {Report}", file.Id, message);
            }
            else
            {
                File.Move(input, glb);
                await MarkAsync(current, "skipped", null, "Already .glb", ct);
            }

            // 2. Stats (and a check that the file really is a usable glTF)
            current = PipelineSteps.Stats;
            ModelStats stats = null!;
            await StepAsync(current, () =>
            {
                stats = GltfStats.Read(glb, file.ModelId);
                var maps = JsonSerializer.Deserialize<List<string>>(stats.AvailableMaps)!;
                return Task.FromResult($"{stats.Triangles:N0} triangles · {stats.Meshes} meshes · {stats.Materials} materials · maps: {(maps.Count == 0 ? "none" : string.Join(", ", maps))}");
            }, ct);

            // 3. Optimize (+ phone copy)
            current = PipelineSteps.Optimize;
            string optimized = Path.Combine(work, "optimized.glb"), mobile = Path.Combine(work, "mobile.glb");
            var makeMobile = stats.Triangles > PipelineSteps.MobileTriangleThreshold;
            await StepAsync(current, async () =>
            {
                await GltfTransformAsync(["optimize", glb, optimized, "--compress", "meshopt", "--texture-compress", "webp",
                    "--texture-size", _o.MaxTextureSize.ToString()], timeout, ct);
                if (makeMobile)
                {
                    var simplified = Path.Combine(work, "simplified.glb");
                    await GltfTransformAsync(["simplify", glb, simplified, "--ratio", "0.5", "--error", "0.01"], timeout, ct);
                    await GltfTransformAsync(["optimize", simplified, mobile, "--compress", "meshopt", "--texture-compress", "webp",
                        "--texture-size", (_o.MaxTextureSize / 2).ToString()], timeout, ct);
                }
                var size = new FileInfo(optimized).Length;
                return $"{Mb(file.SizeBytes)} → {Mb(size)}" + (makeMobile ? $" · phone copy {Mb(new FileInfo(mobile).Length)}" : "");
            }, ct);

            // Save results: the new optimized (and phone) files become current; stats replace the old ones.
            await StoreAsync(file, FileKind.Optimized, optimized, ct);
            if (makeMobile) await StoreAsync(file, FileKind.Mobile, mobile, ct);
            else await RetireAsync(file.ModelId, FileKind.Mobile, ct);
            var existing = await db.ModelStats.FirstOrDefaultAsync(s => s.ModelId == file.ModelId, ct);
            if (existing is null) db.ModelStats.Add(stats);
            else db.Entry(existing).CurrentValues.SetValues(stats);

            _job.Status = JobStatus.Succeeded;
            _job.FinishedAt = DateTimeOffset.UtcNow;
            await SaveAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(e, "Processing job {Job} failed at {Step}", jobId, current);
            db.ChangeTracker.Clear();
            _job = await db.ProcessingJobs.FirstAsync(j => j.Id == jobId, CancellationToken.None);
            var reason = e switch
            {
                ToolFailedException or InvalidDataException => e.Message,
                _ => $"Unexpected error: {e.Message}",
            };
            Mark(current, "failed", null, reason);
            foreach (var later in PipelineSteps.All.SkipWhile(s => s != current).Skip(1)) Mark(later, "skipped", null, null);
            _job.Status = JobStatus.Failed;
            _job.Error = reason.Length > 2000 ? reason[..2000] : reason;
            _job.FinishedAt = DateTimeOffset.UtcNow;
            await SaveAsync(CancellationToken.None);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch { /* best effort */ }
        }
    }

    private async Task<string> RunBlenderAsync(string script, string input, string output, TimeSpan timeout, CancellationToken ct)
    {
        string text;
        try
        {
            text = await Tool.RunAsync(_o.Blender, ["-b", "--factory-startup", "--python-exit-code", "3", "--python", script, "--", input, output], timeout, ct);
        }
        catch (ToolFailedException e) when (e.Message.Contains("REPORT "))
        {
            throw new ToolFailedException(ReportError(e.Message) ?? e.Message);
        }
        var report = text.Split('\n').LastOrDefault(l => l.StartsWith("REPORT "))?["REPORT ".Length..].Trim();
        if (report is null || !File.Exists(output)) throw new ToolFailedException("Blender did not produce a .glb: " + Tool.Tail(text));
        using var doc = JsonDocument.Parse(report);
        var missing = doc.RootElement.TryGetProperty("missingTextures", out var m) ? m.GetArrayLength() : 0;
        return $"Blender: {doc.RootElement.GetProperty("meshes").GetInt32()} meshes, {doc.RootElement.GetProperty("triangles").GetInt32():N0} triangles"
               + (missing > 0 ? $" · {missing} texture(s) missing — export the .fbx with embedded textures" : "");
    }

    private static string? ReportError(string text)
    {
        var line = text.Split('\n').FirstOrDefault(l => l.Contains("REPORT {"));
        if (line is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(line[(line.IndexOf("REPORT ", StringComparison.Ordinal) + 7)..]);
            return doc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task GltfTransformAsync(string[] args, TimeSpan timeout, CancellationToken ct) =>
        Tool.RunAsync(_o.GltfTransform, args, timeout, ct);

    private async Task StoreAsync(ModelFile source, FileKind kind, string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var key = await storage.SaveAsync(stream, ".glb", ct);
        await RetireAsync(source.ModelId, kind, ct);
        db.ModelFiles.Add(new ModelFile
        {
            ModelId = source.ModelId,
            Kind = kind,
            Format = "glb",
            StorageKey = key,
            SizeBytes = stream.Length,
            Version = source.Version,
        });
    }

    private async Task RetireAsync(Guid modelId, FileKind kind, CancellationToken ct)
    {
        foreach (var f in await db.ModelFiles.Where(f => f.ModelId == modelId && f.Kind == kind && f.IsCurrent).ToListAsync(ct))
            f.IsCurrent = false;
    }

    /// <summary>Runs one step, timing it and saving its status before and after.</summary>
    private async Task<string> StepAsync(string name, Func<Task<string>> body, CancellationToken ct)
    {
        await MarkAsync(name, "running", null, null, ct);
        var watch = Stopwatch.StartNew();
        var message = await body();
        await MarkAsync(name, "done", watch.ElapsedMilliseconds, message, ct);
        return message;
    }

    private async Task MarkAsync(string name, string status, long? ms, string? message, CancellationToken ct)
    {
        Mark(name, status, ms, message);
        await SaveAsync(ct);
    }

    private void Mark(string name, string status, long? ms, string? message)
    {
        var step = new JobStepDto(name, status, ms, message);
        var i = _steps.FindIndex(s => s.Name == name);
        if (i >= 0) _steps[i] = step;
        else _steps.Add(step);
        _job.Steps = JsonSerializer.Serialize(_steps, Json);
    }

    private Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static string Mb(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}

/// <summary>Sample recurring job proving the worker is alive.</summary>
public class Heartbeat(ILogger<Heartbeat> log)
{
    public void Run() => log.LogInformation("Worker heartbeat at {Time:O}", DateTimeOffset.UtcNow);
}
