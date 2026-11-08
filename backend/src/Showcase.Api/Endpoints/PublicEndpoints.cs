using Microsoft.EntityFrameworkCore;
using Showcase.Application;
using Showcase.Domain;
using Showcase.Infrastructure;

namespace Showcase.Api.Endpoints;

/// <summary>Read-only data for the Showcase build. Only published models are visible.</summary>
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

        g.MapGet("/models", async (AppDbContext db, CancellationToken ct) =>
        {
            var models = await ModelEndpoints.WithDetails(db.Models.AsNoTracking())
                .Where(m => m.Status == ModelStatus.Published)
                .OrderByDescending(m => m.IsFeatured).ThenBy(m => m.SortOrder)
                .ToListAsync(ct);
            return models.Select(ModelEndpoints.ToSummary);
        });

        g.MapGet("/models/{slug}", async (string slug, AppDbContext db, CancellationToken ct) =>
        {
            var m = await ModelEndpoints.WithDetails(db.Models.AsNoTracking())
                .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == ModelStatus.Published, ct);
            return m is null ? Results.NotFound() : Results.Ok(ModelEndpoints.ToSummary(m));
        }).Produces<ModelSummaryDto>();
    }
}
