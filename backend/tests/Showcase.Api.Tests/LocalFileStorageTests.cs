using Microsoft.Extensions.Options;
using Showcase.Infrastructure;

namespace Showcase.Api.Tests;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "showcase-storage", Guid.NewGuid().ToString("N"));
    private LocalFileStorage Storage => new(Options.Create(new StorageOptions { Root = _root }));

    [Fact]
    public async Task Saves_under_a_guid_name_and_reads_back()
    {
        var key = await Storage.SaveAsync(new MemoryStream([1, 2, 3]), ".GLB");

        Assert.Matches(@"^\d{4}/\d{2}/[0-9a-f]{32}\.glb$", key);
        Assert.True(Storage.Exists(key));
        await using var read = Storage.OpenRead(key);
        var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.ToArray());
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("2026/../../etc/passwd")]
    public void Refuses_keys_outside_the_root(string key)
    {
        Assert.Throws<ArgumentException>(() => Storage.Exists(key));
    }

    [Fact]
    public async Task Rejects_strange_extensions()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Storage.SaveAsync(new MemoryStream(), ".g/lb"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
