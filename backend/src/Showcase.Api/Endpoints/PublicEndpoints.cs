using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Showcase.Application;
using Showcase.Domain;
using Showcase.Infrastructure;

namespace Showcase.Api.Endpoints;

/// <summary>
/// Read-only data for the Showcase build. Only published models — and their files and images — are visible.
/// </summary>
public static class PublicEndpoints
{
    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/public").WithTags("Public").AllowAnonymous();

        g.MapGet("/categories", async (AppDbContext db, CancellationToken ct) =>
            await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn)
                .Select(c => new CategoryDto(c.Id, c.NameEn, c.NameVi, c.Slug, c.SortOrder))
                .ToListAsync(ct));

        g.MapGet("/tags", (AppDbContext db, CancellationToken ct) => TagEndpoints.ListAsync(db, ct));

        g.MapGet("/profile", (AppDbContext db, CancellationToken ct) => ProfileEndpoints.GetAsync(db, ct));
        g.MapPublicProfileFiles();

        // In Showcase order: featured first, then the order set in the Studio.
        g.MapGet("/models", async (AppDbContext db, CancellationToken ct) =>
        {
            var models = await Published(db)
                .OrderByDescending(m => m.IsFeatured).ThenBy(m => m.SortOrder)
                .ToListAsync(ct);
            return models.Select(ToPublic).ToList();
        });

        g.MapGet("/models/{slug}", async (string slug, AppDbContext db, CancellationToken ct) =>
        {
            var m = await Published(db).FirstOrDefaultAsync(x => x.Slug == slug, ct);
            return m is null ? Results.NotFound() : Results.Ok(ToPublic(m));
        }).Produces<PublicModelDto>();

        g.MapGet("/models/{slug}/model.glb", async (string slug, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var m = await Published(db).FirstOrDefaultAsync(x => x.Slug == slug, ct);
            var file = m is null ? null : ViewableFile(m);
            if (file is null || !storage.Exists(file.StorageKey)) return Results.NotFound();
            return Results.Stream(storage.OpenRead(file.StorageKey), "model/gltf-binary", enableRangeProcessing: true);
        });

        g.MapGet("/models/{slug}/mobile.glb", async (string slug, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var m = await Published(db).FirstOrDefaultAsync(x => x.Slug == slug, ct);
            var file = m?.Files.FirstOrDefault(f => f.IsCurrent && f.Kind == FileKind.Mobile);
            if (file is null || !storage.Exists(file.StorageKey)) return Results.NotFound();
            return Results.Stream(storage.OpenRead(file.StorageKey), "model/gltf-binary", enableRangeProcessing: true);
        });

        g.MapGet("/images/{id:guid}", async (Guid id, string? size, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var image = await db.ModelImages.AsNoTracking()
                .Where(i => i.Id == id && db.Models.Any(m => m.Id == i.ModelId && m.Status == ModelStatus.Published))
                .FirstOrDefaultAsync(ct);
            return image is null ? Results.NotFound() : ImageEndpoints.StreamImage(storage, image, size);
        });
    }

    private static IQueryable<Model> Published(AppDbContext db) =>
        ModelEndpoints.WithDetails(db.Models.AsNoTracking()).Include(m => m.Files)
            .Where(m => m.Status == ModelStatus.Published);

    /// <summary>The optimized .glb once GĐ2 processing exists, else the uploaded .glb.</summary>
    private static ModelFile? ViewableFile(Model m) =>
        m.Files.FirstOrDefault(f => f.IsCurrent && f.Kind == FileKind.Optimized && f.Format == "glb")
        ?? m.Files.FirstOrDefault(f => f.IsCurrent && f.Kind == FileKind.Original && f.Format == "glb");

    private static string PublicImageUrl(ModelImage i) => $"/api/public/images/{i.Id}";

    internal static PublicModelDto ToPublic(Model m)
    {
        var file = ViewableFile(m);
        var original = m.Files.FirstOrDefault(f => f.IsCurrent && f.Kind == FileKind.Original);
        var phone = m.Files.FirstOrDefault(f => f.IsCurrent && f.Kind == FileKind.Mobile);
        var thumb = m.Images.FirstOrDefault(i => i.Kind == ImageKind.Thumbnail);
        var s = m.Stats;
        return new PublicModelDto(
            m.Id,
            m.Slug,
            m.IsFeatured,
            m.SortOrder,
            m.Category?.Slug,
            m.Tags.Select(t => t.Slug).Order().ToList(),
            m.Translations.OrderBy(t => t.Locale).Select(t => new TranslationDto(t.Locale, t.Title, t.Description)).ToList(),
            m.Software,
            m.HoursSpent,
            m.CompletedAt,
            s is null ? null : new PublicStatsDto(s.Triangles, s.Vertices, s.Meshes, s.Materials, s.Textures, s.MaxTextureSize, s.HasAnimation,
                JsonSerializer.Deserialize<List<string>>(s.AvailableMaps) ?? []),
            ImageEndpoints.ReadSettings(m.ViewerSettings),
            file is null ? null : $"/api/public/models/{m.Slug}/model.glb",
            file?.SizeBytes,
            original?.Format,
            phone is null ? null : $"/api/public/models/{m.Slug}/mobile.glb",
            phone?.SizeBytes,
            thumb is null ? null : PublicImageUrl(thumb),
            m.Images.Where(i => i.Kind != ImageKind.Thumbnail).OrderBy(i => i.SortOrder)
                .Select(i => ImageEndpoints.ToDto(i) with
                {
                    Url = PublicImageUrl(i),
                    SmallUrl = i.SmallStorageKey is null ? null : PublicImageUrl(i) + "?size=small",
                }).ToList(),
            m.UpdatedAt);
    }
}
