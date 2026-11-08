using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Showcase.Application;
using Showcase.Domain;
using Showcase.Infrastructure;

namespace Showcase.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/categories").WithTags("Admin · Categories").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            await db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn)
                .Select(c => new CategoryDto(c.Id, c.NameEn, c.NameVi, c.Slug, c.SortOrder))
                .ToListAsync(ct));

        g.MapPost("/", async (UpsertCategoryRequest req, IValidator<UpsertCategoryRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            if (await db.Categories.AnyAsync(c => c.Slug == req.Slug, ct))
                return Results.Conflict(new { error = $"Slug '{req.Slug}' is already used." });

            var c = new Category { NameEn = req.NameEn, NameVi = req.NameVi, Slug = req.Slug, SortOrder = req.SortOrder };
            db.Categories.Add(c);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/categories/{c.Id}", new CategoryDto(c.Id, c.NameEn, c.NameVi, c.Slug, c.SortOrder));
        });

        g.MapPut("/{id:guid}", async (Guid id, UpsertCategoryRequest req, IValidator<UpsertCategoryRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            var c = await db.Categories.FindAsync([id], ct);
            if (c is null) return Results.NotFound();
            if (await db.Categories.AnyAsync(x => x.Slug == req.Slug && x.Id != id, ct))
                return Results.Conflict(new { error = $"Slug '{req.Slug}' is already used." });

            (c.NameEn, c.NameVi, c.Slug, c.SortOrder) = (req.NameEn, req.NameVi, req.Slug, req.SortOrder);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CategoryDto(c.Id, c.NameEn, c.NameVi, c.Slug, c.SortOrder));
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var c = await db.Categories.FindAsync([id], ct);
            if (c is null) return Results.NotFound();
            db.Categories.Remove(c);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
