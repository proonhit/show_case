using System.Text.Json;
using FluentValidation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Showcase.Application;
using Showcase.Domain;
using Showcase.Infrastructure;

namespace Showcase.Api.Endpoints;

/// <summary>Per-model viewer settings, the thumbnail and extra images (renders, wireframes, process shots).</summary>
public static class ImageEndpoints
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        var models = app.MapGroup("/api/admin/models/{id:guid}").WithTags("Admin · Models").RequireAuthorization();

        models.MapPut("/viewer", async (Guid id, ViewerSettingsDto req, IValidator<ViewerSettingsDto> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            var m = await db.Models.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();
            m.ViewerSettings = JsonSerializer.Serialize(req, Json);
            m.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(req);
        }).Produces<ViewerSettingsDto>();

        // Multipart: "file" (jpg/png/webp ≤ 10 MB) + "kind" (Thumbnail, Render, Wireframe, Process).
        // A new thumbnail replaces the old one. Antiforgery off: the session cookie is SameSite=Strict.
        models.MapPost("/images", async (Guid id, IFormFile file, [FromForm] string kind, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            if (!Enum.TryParse<ImageKind>(kind, ignoreCase: false, out var imageKind) || !Enum.IsDefined(imageKind))
                return Problem("kind", "Kind must be Thumbnail, Render, Wireframe or Process.");
            var m = await db.Models.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();
            if (file.Length == 0 || file.Length > MaxImageBytes) return Problem("file", "Image must be between 1 byte and 10 MB.");

            await using var stream = file.OpenReadStream();
            var head = new byte[16];
            var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
            var type = ProfileEndpoints.Sniff(head.AsSpan(0, read));
            if (type is not ("jpg" or "png" or "webp")) return Problem("file", "Image must be a JPG, PNG or WebP file.");
            stream.Position = 0;

            // Stored as WebP: a full copy (≤ 2560 px) and a small one (≤ 640 px) for grids. Camera metadata
            // (EXIF/XMP, which can include GPS) is dropped.
            MemoryStream full, small;
            try
            {
                using var decoded = await Image.LoadAsync(stream, ct);
                decoded.Mutate(x => x.AutoOrient());
                full = await EncodeWebpAsync(decoded, 2560, 88, ct);
                small = await EncodeWebpAsync(decoded, 640, 80, ct);
            }
            catch (Exception e) when (e is ImageFormatException or UnknownImageFormatException or InvalidImageContentException)
            {
                return Problem("file", "The image could not be read (damaged or not really a JPG/PNG/WebP).");
            }
            var key = await storage.SaveAsync(full, ".webp", ct);
            var smallKey = await storage.SaveAsync(small, ".webp", ct);

            var replaced = imageKind == ImageKind.Thumbnail ? m.Images.Where(i => i.Kind == ImageKind.Thumbnail).ToList() : new List<ModelImage>();
            foreach (var old in replaced) db.ModelImages.Remove(old);
            var image = new ModelImage
            {
                ModelId = id,
                Kind = imageKind,
                StorageKey = key,
                SmallStorageKey = smallKey,
                SortOrder = m.Images.Count == 0 ? 0 : m.Images.Max(i => i.SortOrder) + 1,
            };
            db.ModelImages.Add(image);
            m.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            foreach (var old in replaced) await DeleteFilesAsync(storage, old, ct);
            return Results.Created(UrlOf(image), ToDto(image));
        }).DisableAntiforgery().Produces<ModelImageDto>(StatusCodes.Status201Created);

        models.MapPut("/images/order", async (Guid id, ReorderRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var images = await db.ModelImages.Where(i => i.ModelId == id).OrderBy(i => i.SortOrder).ToListAsync(ct);
            var position = req.Ids.Distinct().Select((x, i) => (x, i)).ToDictionary(p => p.x, p => p.i);
            var ordered = images.OrderBy(i => position.TryGetValue(i.Id, out var p) ? p : int.MaxValue).ThenBy(i => i.SortOrder).ToList();
            for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        var images = app.MapGroup("/api/admin/images/{imageId:guid}").WithTags("Admin · Models").RequireAuthorization();

        images.MapPatch("/", async (Guid imageId, UpdateImageRequest req, IValidator<UpdateImageRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            var image = await db.ModelImages.FindAsync([imageId], ct);
            if (image is null) return Results.NotFound();
            image.CaptionEn = string.IsNullOrWhiteSpace(req.CaptionEn) ? null : req.CaptionEn.Trim();
            image.CaptionVi = string.IsNullOrWhiteSpace(req.CaptionVi) ? null : req.CaptionVi.Trim();
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(image));
        }).Produces<ModelImageDto>();

        images.MapDelete("/", async (Guid imageId, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var image = await db.ModelImages.FindAsync([imageId], ct);
            if (image is null) return Results.NotFound();
            db.ModelImages.Remove(image);
            await db.SaveChangesAsync(ct);
            await DeleteFilesAsync(storage, image, ct);
            return Results.NoContent();
        });

        images.MapGet("/content", async (Guid imageId, string? size, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var image = await db.ModelImages.AsNoTracking().FirstOrDefaultAsync(i => i.Id == imageId, ct);
            return image is null ? Results.NotFound() : StreamImage(storage, image, size);
        });
    }

    internal static string UrlOf(ModelImage i) => $"/api/admin/images/{i.Id}/content";

    internal static ModelImageDto ToDto(ModelImage i) =>
        new(i.Id, i.Kind.ToString(), UrlOf(i), i.CaptionEn, i.CaptionVi, i.SortOrder,
            i.SmallStorageKey is null ? null : UrlOf(i) + "?size=small");

    /// <summary>The full image, or the small copy for ?size=small (falls back to the full one for older images).</summary>
    internal static IResult StreamImage(IFileStorage storage, ModelImage image, string? size)
    {
        var key = size == "small" && image.SmallStorageKey is { } small && storage.Exists(small) ? small : image.StorageKey;
        if (!storage.Exists(key)) return Results.NotFound();
        var mime = System.IO.Path.GetExtension(key) switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return Results.Stream(storage.OpenRead(key), mime, enableRangeProcessing: true);
    }

    private static async Task DeleteFilesAsync(IFileStorage storage, ModelImage image, CancellationToken ct)
    {
        await storage.DeleteAsync(image.StorageKey, ct);
        if (image.SmallStorageKey is not null) await storage.DeleteAsync(image.SmallStorageKey, ct);
    }

    private static async Task<MemoryStream> EncodeWebpAsync(Image source, int maxSide, int quality, CancellationToken ct)
    {
        using var copy = source.Clone(x =>
        {
            if (source.Width > maxSide || source.Height > maxSide)
                x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(maxSide, maxSide) });
        });
        copy.Metadata.ExifProfile = null;
        copy.Metadata.XmpProfile = null;
        var ms = new MemoryStream();
        await copy.SaveAsWebpAsync(ms, new WebpEncoder { Quality = quality }, ct);
        ms.Position = 0;
        return ms;
    }

    /// <summary>Stored JSON → settings; anything unreadable falls back to the defaults.</summary>
    internal static ViewerSettingsDto ReadSettings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ViewerSettingsDto>(json, Json) ?? new ViewerSettingsDto();
        }
        catch (JsonException)
        {
            return new ViewerSettingsDto();
        }
    }

    private static IResult Problem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
