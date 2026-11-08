namespace Showcase.Application;

/// <summary>
/// Where uploaded files live. The MVP stores them on the server's disk (<c>LocalFileStorage</c>);
/// moving to S3 later only needs another implementation.
/// </summary>
public interface IFileStorage
{
    /// <summary>Saves the stream and returns a relative storage key such as <c>2026/11/{guid}.glb</c>.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);

    Stream OpenRead(string key);

    Task DeleteAsync(string key, CancellationToken ct = default);

    bool Exists(string key);
}
