namespace Showcase.Application;

public record ModelFileDto(Guid Id, string Kind, string Format, long SizeBytes, int Version, bool IsCurrent, DateTimeOffset UploadedAt);

/// <summary>Background work run by the Worker (Hangfire) after a model file is uploaded.</summary>
public interface IModelFileProcessor
{
    /// <summary>
    /// Runs one <c>ProcessingJob</c>: convert (.fbx → .glb), read stats, optimize (+ a phone version).
    /// Failures are recorded on the job (with the step and a readable message), never retried:
    /// a broken file stays broken (the Worker turns Hangfire retries off).
    /// </summary>
    Task ProcessAsync(Guid jobId, CancellationToken ct);
}

public record JobStepDto(string Name, string Status, long? DurationMs = null, string? Message = null);

public record ProcessingJobDto(
    Guid Id,
    Guid ModelId,
    Guid ModelFileId,
    string Status,
    IReadOnlyList<JobStepDto> Steps,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);

public static class PipelineSteps
{
    public const string Upload = "upload";
    public const string Convert = "convert";
    public const string Stats = "stats";
    public const string Optimize = "optimize";
    public static readonly string[] All = [Upload, Convert, Stats, Optimize];

    /// <summary>Above this many triangles a lighter copy is made for phones.</summary>
    public const int MobileTriangleThreshold = 150_000;
}

public static class UploadRules
{
    public static readonly string[] Formats = ["glb", "fbx"];

    /// <summary>Returns "glb" / "fbx" from a file name, or null when the extension is not accepted.</summary>
    public static string? FormatOf(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").TrimStart('.').ToLowerInvariant();
        return Formats.Contains(ext) ? ext : null;
    }

    /// <summary>Checks the first bytes of the file instead of trusting the extension.</summary>
    public static bool MagicMatches(string format, ReadOnlySpan<byte> head) => format switch
    {
        "glb" => head.StartsWith("glTF"u8),
        "fbx" => head.StartsWith("Kaydara FBX Binary"u8) || head.StartsWith("; FBX"u8),
        _ => false,
    };
}
