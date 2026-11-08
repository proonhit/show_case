using Microsoft.EntityFrameworkCore;
using Showcase.Application;
using Showcase.Infrastructure;

namespace Showcase.Worker;

/// <summary>
/// GĐ1 placeholder: checks the uploaded file is on disk and logs it.
/// GĐ2 adds Blender conversion (.fbx → .glb), gltf-transform optimization and SharpGLTF stats.
/// </summary>
public class ModelFileProcessor(AppDbContext db, IFileStorage storage, ILogger<ModelFileProcessor> log) : IModelFileProcessor
{
    public async Task ProcessAsync(Guid modelFileId, CancellationToken ct)
    {
        var file = await db.ModelFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == modelFileId, ct)
                   ?? throw new InvalidOperationException($"Model file {modelFileId} not found.");

        if (!storage.Exists(file.StorageKey))
            throw new FileNotFoundException($"Stored file is missing for {modelFileId}.", file.StorageKey);

        log.LogInformation("Received {Format} file {FileId} ({Size:N0} bytes) for model {ModelId}; processing arrives in GĐ2",
            file.Format, file.Id, file.SizeBytes, file.ModelId);
    }
}

/// <summary>Sample recurring job proving the worker is alive.</summary>
public class Heartbeat(ILogger<Heartbeat> log)
{
    public void Run() => log.LogInformation("Worker heartbeat at {Time:O}", DateTimeOffset.UtcNow);
}
