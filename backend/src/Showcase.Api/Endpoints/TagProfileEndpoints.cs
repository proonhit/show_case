using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Showcase.Application;
using Showcase.Domain;
using Showcase.Infrastructure;

namespace Showcase.Api.Endpoints;

public static class TagEndpoints
{
    public static void MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/tags").WithTags("Admin · Tags").RequireAuthorization();

        g.MapGet("/", (AppDbContext db, CancellationToken ct) => ListAsync(db, ct));

        g.MapPost("/", async (UpsertTagRequest req, IValidator<UpsertTagRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            if (await db.Tags.AnyAsync(t => t.Slug == req.Slug, ct))
                return Results.Conflict(new { error = $"Slug '{req.Slug}' is already used." });

            var t = new Tag { NameEn = req.NameEn, NameVi = req.NameVi, Slug = req.Slug };
            db.Tags.Add(t);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/tags/{t.Id}", ToDto(t));
        }).Produces<TagDto>(StatusCodes.Status201Created);

        g.MapPut("/{id:guid}", async (Guid id, UpsertTagRequest req, IValidator<UpsertTagRequest> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            var t = await db.Tags.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            if (await db.Tags.AnyAsync(x => x.Slug == req.Slug && x.Id != id, ct))
                return Results.Conflict(new { error = $"Slug '{req.Slug}' is already used." });

            (t.NameEn, t.NameVi, t.Slug) = (req.NameEn, req.NameVi, req.Slug);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(t));
        }).Produces<TagDto>();

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var t = await db.Tags.FindAsync([id], ct);
            if (t is null) return Results.NotFound();
            db.Tags.Remove(t); // the ModelTags join rows go with it
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    internal static Task<List<TagDto>> ListAsync(AppDbContext db, CancellationToken ct) =>
        db.Tags.AsNoTracking().OrderBy(t => t.NameEn).Select(t => new TagDto(t.Id, t.NameEn, t.NameVi, t.Slug)).ToListAsync(ct);

    private static TagDto ToDto(Tag t) => new(t.Id, t.NameEn, t.NameVi, t.Slug);
}

/// <summary>The single About-page profile (row Id = 1).</summary>
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/profile").WithTags("Admin · Profile").RequireAuthorization();

        g.MapGet("/", (AppDbContext db, CancellationToken ct) => GetAsync(db, ct));

        g.MapPut("/", async (ProfileDto req, IValidator<ProfileDto> v, AppDbContext db, CancellationToken ct) =>
        {
            if (await v.ValidateRequestAsync(req, ct) is { } invalid) return invalid;
            var p = await db.SiteProfiles.FindAsync([1], ct);
            if (p is null) db.SiteProfiles.Add(p = new SiteProfile());

            p.DisplayName = req.DisplayName;
            (p.HeadlineEn, p.HeadlineVi, p.BioEn, p.BioVi) = (req.HeadlineEn, req.HeadlineVi, req.BioEn, req.BioVi);
            p.Links = JsonSerializer.Serialize(req.Links);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(p));
        }).Produces<ProfileDto>();
    }

    internal static async Task<ProfileDto> GetAsync(AppDbContext db, CancellationToken ct) =>
        ToDto(await db.SiteProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == 1, ct) ?? new SiteProfile());

    private static ProfileDto ToDto(SiteProfile p) => new(
        p.DisplayName, p.HeadlineEn, p.HeadlineVi, p.BioEn, p.BioVi,
        JsonSerializer.Deserialize<Dictionary<string, string>>(p.Links) ?? []);
}
