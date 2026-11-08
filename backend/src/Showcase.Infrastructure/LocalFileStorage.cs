using Microsoft.Extensions.Options;
using Showcase.Application;

namespace Showcase.Infrastructure;

public class StorageOptions
{
    /// <summary>Root folder for uploads, e.g. <c>/data/uploads</c>.</summary>
    public string Root { get; set; } = "data/uploads";
}

/// <summary>Stores files on the server's disk under GUID names, outside the code folder.</summary>
public class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private string Root => Path.GetFullPath(options.Value.Root);

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        extension = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        if (extension.Length > 8 || extension.Skip(1).Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Invalid file extension.", nameof(extension));

        var now = DateTimeOffset.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Stream OpenRead(string key) =>
        new FileStream(Resolve(key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public bool Exists(string key) => File.Exists(Resolve(key));

    /// <summary>Maps a key to a full path and refuses anything that escapes the root folder.</summary>
    private string Resolve(string key)
    {
        var root = Root;
        var full = Path.GetFullPath(Path.Combine(root, key));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Storage key escapes the upload root.", nameof(key));
        return full;
    }
}
