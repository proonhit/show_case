using System.Net;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Showcase.Application;
using Showcase.Domain;
using Showcase.Infrastructure;
using tusdotnet;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

namespace Showcase.Api.Endpoints;

public class UploadOptions
{
    /// <summary>Where partial tus uploads are kept until complete.</summary>
    public string TempPath { get; set; } = Path.Combine(Path.GetTempPath(), "showcase-tus");

    public int MaxMegabytes { get; set; } = 100;
}

/// <summary>
/// Resumable, chunked uploads (tus protocol) for .glb / .fbx files.
/// Client metadata: <c>modelId</c> and <c>filename</c>. When an upload completes the file is
/// checked by its first bytes, moved into <see cref="IFileStorage"/>, recorded as the model's
/// current original file, and a processing job is queued for the Worker.
/// </summary>
public static class UploadEndpoints
{
    public const string Path = "/api/admin/uploads";

    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapTus(Path, http => Task.FromResult(CreateConfiguration(http)));

        app.MapGet("/api/admin/models/{id:guid}/files", async (Guid id, AppDbContext db, CancellationToken ct) =>
            await db.ModelFiles.AsNoTracking().Where(f => f.ModelId == id)
                .OrderByDescending(f => f.UploadedAt)
                .Select(f => new ModelFileDto(f.Id, f.Kind.ToString(), f.Format, f.SizeBytes, f.Version, f.IsCurrent, f.UploadedAt))
                .ToListAsync(ct))
            .WithTags("Admin · Models")
            .RequireAuthorization();
    }

    private static DefaultTusConfiguration CreateConfiguration(HttpContext http)
    {
        var options = http.RequestServices.GetRequiredService<IOptions<UploadOptions>>().Value;
        Directory.CreateDirectory(options.TempPath);

        return new DefaultTusConfiguration
        {
            Store = new TusDiskStore(options.TempPath),
            MaxAllowedUploadSizeInBytesLong = options.MaxMegabytes * 1024L * 1024L,
            Events = new Events
            {
                OnAuthorizeAsync = ctx =>
                {
                    if (ctx.HttpContext.User.Identity?.IsAuthenticated != true)
                        ctx.FailRequest(HttpStatusCode.Unauthorized);
                    return Task.CompletedTask;
                },
                OnBeforeCreateAsync = async ctx =>
                {
                    if (!TryReadMetadata(ctx.Metadata, out var modelId, out var format, out var error))
                    {
                        ctx.FailRequest(error);
                        return;
                    }
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    if (!await db.Models.AnyAsync(m => m.Id == modelId, ctx.CancellationToken))
                        ctx.FailRequest($"Model {modelId} not found.");
                },
                OnFileCompleteAsync = ctx => OnFileCompleteAsync(ctx.HttpContext, ctx.Store, ctx.FileId, ctx.CancellationToken),
            },
        };
    }

    private static async Task OnFileCompleteAsync(HttpContext http, ITusStore store, string fileId, CancellationToken ct)
    {
        var sp = http.RequestServices;
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(UploadEndpoints));
        var file = await ((ITusReadableStore)store).GetFileAsync(fileId, ct)
                   ?? throw new InvalidOperationException($"tus file {fileId} not found.");
        var metadata = await file.GetMetadataAsync(ct);
        TryReadMetadata(metadata, out var modelId, out var format, out _);

        try
        {
            await using var content = await file.GetContentAsync(ct);
            var head = new byte[32];
            var read = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
            if (!UploadRules.MagicMatches(format, head.AsSpan(0, read)))
            {
                log.LogWarning("Rejected upload {FileId}: content is not a valid .{Format} file", fileId, format);
                return;
            }
            content.Position = 0;

            var storage = sp.GetRequiredService<IFileStorage>();
            var key = await storage.SaveAsync(content, "." + format, ct);

            var db = sp.GetRequiredService<AppDbContext>();
            var previous = await db.ModelFiles
                .Where(f => f.ModelId == modelId && f.Kind == FileKind.Original)
                .ToListAsync(ct);
            foreach (var p in previous) p.IsCurrent = false;

            var modelFile = new ModelFile
            {
                ModelId = modelId,
                Kind = FileKind.Original,
                Format = format,
                StorageKey = key,
                SizeBytes = content.Length,
                Version = previous.Count == 0 ? 1 : previous.Max(p => p.Version) + 1,
            };
            db.ModelFiles.Add(modelFile);
            await db.SaveChangesAsync(ct);

            sp.GetRequiredService<IBackgroundJobClient>()
                .Enqueue<IModelFileProcessor>(p => p.ProcessAsync(modelFile.Id, CancellationToken.None));
            log.LogInformation("Stored {Format} upload for model {ModelId} as {Key}", format, modelId, key);
        }
        finally
        {
            if (store is ITusTerminationStore termination)
                await termination.DeleteFileAsync(fileId, ct);
        }
    }

    private static bool TryReadMetadata(Dictionary<string, Metadata> metadata, out Guid modelId, out string format, out string error)
    {
        modelId = Guid.Empty;
        format = "";
        error = "";
        if (!metadata.TryGetValue("modelId", out var m) || !Guid.TryParse(m.GetString(Encoding.UTF8), out modelId))
        {
            error = "Upload metadata 'modelId' is required.";
            return false;
        }
        if (!metadata.TryGetValue("filename", out var f) || UploadRules.FormatOf(f.GetString(Encoding.UTF8)) is not { } fmt)
        {
            error = "Upload metadata 'filename' must end with .glb or .fbx.";
            return false;
        }
        format = fmt;
        return true;
    }
}
