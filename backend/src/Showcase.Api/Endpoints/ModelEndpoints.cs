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
            var models = await WithDetails(db.Models.AsNoTracking())
                .OrderBy(m => m.SortOrder).ThenByDescending(m => m.UpdatedAt)
                .ToListAsync(ct);
            return models.Select(ToSummary);
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var m = await WithDetails(db.Models.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
            return m is null ? Results.NotFound() : Results.Ok(ToDetail(m));
        }).Produces<ModelDetailDto>();

        g.MapPost("/", async (UpsertModelRequest req, IValidator<UpsertModelRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            if (await CheckReferencesAsync(db, req, null, ct) is { } problem) return problem;

            var nextOrder = await db.Models.Select(m => (int?)m.SortOrder).MaxAsync(ct) ?? -1;
            var model = new Model { Slug = req.Slug, SortOrder = nextOrder + 1 };
            await ApplyAsync(db, model, req, ct);
            db.Models.Add(model);
            await db.SaveChangesAsync(ct);
            await db.Entry(model).Reference(m => m.Category).LoadAsync(ct);
            return Results.Created($"/api/admin/models/{model.Id}", ToSummary(model));
        }).Produces<ModelSummaryDto>(StatusCodes.Status201Created);

        g.MapPut("/{id:guid}", async (Guid id, UpsertModelRequest req, IValidator<UpsertModelRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            var m = await WithDetails(db.Models).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.NotFound();
            if (await CheckReferencesAsync(db, req, id, ct) is { } problem) return problem;

            await ApplyAsync(db, m, req, ct);
            m.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDetail(m));
        }).Produces<ModelDetailDto>();

        // Drag-and-drop order on the Studio list: ids in display order; models not listed keep their place after them.
        g.MapPut("/order", async (ReorderModelsRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var models = await db.Models.OrderBy(m => m.SortOrder).ToListAsync(ct);
            var position = req.Ids.Distinct().Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var ordered = models.OrderBy(m => position.TryGetValue(m.Id, out var i) ? i : int.MaxValue).ThenBy(m => m.SortOrder).ToList();
            for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
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

    internal static IQueryable<Model> WithDetails(IQueryable<Model> q) =>
        q.Include(m => m.Translations).Include(m => m.Category).Include(m => m.Stats).Include(m => m.Tags);

    /// <summary>Slug must be free (deleted models keep theirs); category and tags must exist.</summary>
    private static async Task<IResult?> CheckReferencesAsync(AppDbContext db, UpsertModelRequest req, Guid? selfId, CancellationToken ct)
    {
        if (await db.Models.IgnoreQueryFilters().AnyAsync(m => m.Slug == req.Slug && m.Id != selfId, ct))
            return Results.Conflict(new { error = $"Slug '{req.Slug}' is already used." });
        if (req.CategoryId is { } cid && !await db.Categories.AnyAsync(c => c.Id == cid, ct))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["CategoryId"] = ["Category not found."] });
        if (req.TagIds is { Count: > 0 } ids)
        {
            var distinct = ids.Distinct().ToList();
            if (await db.Tags.CountAsync(t => distinct.Contains(t.Id), ct) != distinct.Count)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["TagIds"] = ["One or more tags were not found."] });
        }
        return null;
    }

    /// <summary>Copies the request onto the model. Translations are matched by locale; tags are replaced when given.</summary>
    private static async Task ApplyAsync(AppDbContext db, Model m, UpsertModelRequest req, CancellationToken ct)
    {
        m.Slug = req.Slug;
        m.CategoryId = req.CategoryId;
        m.Software = req.Software;
        m.HoursSpent = req.HoursSpent;
        m.CompletedAt = req.CompletedAt;

        m.Translations.RemoveAll(t => req.Translations.All(r => r.Locale != t.Locale));
        foreach (var r in req.Translations)
        {
            var t = m.Translations.FirstOrDefault(x => x.Locale == r.Locale);
            if (t is null) m.Translations.Add(new ModelTranslation { Locale = r.Locale, Title = r.Title, Description = r.Description });
            else (t.Title, t.Description) = (r.Title, r.Description);
        }

        if (req.TagIds is not null)
        {
            var ids = req.TagIds.Distinct().ToList();
            var tags = await db.Tags.Where(t => ids.Contains(t.Id)).ToListAsync(ct);
            m.Tags.Clear();
            m.Tags.AddRange(tags);
        }
    }

    internal static ModelSummaryDto ToSummary(Model m) => new(
        m.Id,
        m.Slug,
        m.Status.ToString(),
        m.IsFeatured,
        m.SortOrder,
        m.Category?.Slug,
        Translations(m),
        m.Stats?.Triangles,
        m.Tags.Select(t => t.Slug).Order().ToList());

    private static ModelDetailDto ToDetail(Model m) => new(
        m.Id,
        m.Slug,
        m.Status.ToString(),
        m.IsFeatured,
        m.SortOrder,
        m.CategoryId,
        m.Software,
        m.HoursSpent,
        m.CompletedAt,
        Translations(m),
        m.Tags.Select(t => t.Id).ToList(),
        m.UpdatedAt);

    private static List<TranslationDto> Translations(Model m) =>
        m.Translations.OrderBy(t => t.Locale).Select(t => new TranslationDto(t.Locale, t.Title, t.Description)).ToList();
}

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/admin/stats", async (AppDbContext db, CancellationToken ct) =>
        {
            var byStatus = await db.Models.GroupBy(m => m.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
            int Count(ModelStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
            var bytes = await db.ModelFiles.SumAsync(f => (long?)f.SizeBytes, ct) ?? 0;
            return new StudioStatsDto(byStatus.Sum(x => x.Count), Count(ModelStatus.Published), Count(ModelStatus.Draft), Count(ModelStatus.Archived), bytes);
        })
        .WithTags("Admin · Models")
        .RequireAuthorization();
}
