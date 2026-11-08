namespace Showcase.Application;

public record ModelFileDto(Guid Id, string Kind, string Format, long SizeBytes, int Version, bool IsCurrent, DateTimeOffset UploadedAt);

/// <summary>Background work run by the Worker (Hangfire) after a model file is uploaded.</summary>
public interface IModelFileProcessor
{
    /// <summary>Convert (.fbx → .glb), optimize and read stats. Filled in during GĐ2.</summary>
    Task ProcessAsync(Guid modelFileId, CancellationToken ct);
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
