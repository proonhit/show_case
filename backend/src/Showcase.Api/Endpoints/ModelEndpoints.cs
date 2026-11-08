using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Showcase.Application;
using Showcase.Domain;
using Showcase.Infrastructure;

namespace Showcase.Api.Endpoints;

public static class ModelEndpoints
{
    public static void MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/models").WithTags("Admin · Models").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var models = await db.Models.AsNoTracking()
                .Include(m => m.Translations).Include(m => m.Category).Include(m => m.Stats)
                .OrderBy(m => m.SortOrder).ThenByDescending(m => m.UpdatedAt)
                .ToListAsync(ct);
            return models.Select(ToSummary);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var m = await db.Models.AsNoTracking()
                .Include(x => x.Translations).Include(x => x.Category).Include(x => x.Stats)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return m is null ? Results.NotFound() : Results.Ok(ToSummary(m));
        });

        g.MapPost("/", async (CreateModelRequest req, IValidator<CreateModelRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            if (await db.Models.IgnoreQueryFilters().AnyAsync(m => m.Slug == req.Slug, ct))
                return Results.Conflict(new { error = $"Slug '{req.Slug}' is already used." });
            if (req.CategoryId is { } cid && !await db.Categories.AnyAsync(c => c.Id == cid, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["CategoryId"] = ["Category not found."] });

            var nextOrder = await db.Models.Select(m => (int?)m.SortOrder).MaxAsync(ct) ?? -1;
            var model = new Model
            {
                Slug = req.Slug,
                CategoryId = req.CategoryId,
                Software = req.Software,
                HoursSpent = req.HoursSpent,
                CompletedAt = req.CompletedAt,
                SortOrder = nextOrder + 1,
                Translations = req.Translations
                    .Select(t => new ModelTranslation { Locale = t.Locale, Title = t.Title, Description = t.Description })
                    .ToList(),
            };
            db.Models.Add(model);
            await db.SaveChangesAsync(ct);
            await db.Entry(model).Reference(m => m.Category).LoadAsync(ct);
            return Results.Created($"/api/admin/models/{model.Id}", ToSummary(model));
        });

        g.MapPatch("/{id:guid}/status", async (Guid id, UpdateModelStatusRequest req, IValidator<UpdateModelStatusRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            var m = await db.Models.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();

            m.Status = Enum.Parse<ModelStatus>(req.Status);
            m.IsFeatured = req.IsFeatured;
            m.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var m = await db.Models.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();
            m.DeletedAt = DateTimeOffset.UtcNow; // soft delete; files are cleaned up by a later job
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    internal static ModelSummaryDto ToSummary(Model m) => new(
        m.Id,
        m.Slug,
        m.Status.ToString(),
        m.IsFeatured,
        m.SortOrder,
        m.Category?.Slug,
        m.Translations.OrderBy(t => t.Locale).Select(t => new TranslationDto(t.Locale, t.Title, t.Description)).ToList(),
        m.Stats?.Triangles);
}
