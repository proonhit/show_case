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

        // Avatar (jpg/png/webp, ≤ 5 MB) and CV (pdf, ≤ 10 MB): multipart field "file".
        // Antiforgery is off: the session cookie is SameSite=Strict, so other sites cannot post with it.
        g.MapPut("/avatar", (IFormFile file, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
            ReplaceAsync(ProfileFile.Avatar, file, db, storage, ct)).DisableAntiforgery().Produces<ProfileDto>();
        g.MapDelete("/avatar", (AppDbContext db, IFileStorage storage, CancellationToken ct) =>
            ReplaceAsync(ProfileFile.Avatar, null, db, storage, ct)).Produces<ProfileDto>();
        g.MapPut("/cv", (IFormFile file, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
            ReplaceAsync(ProfileFile.Cv, file, db, storage, ct)).DisableAntiforgery().Produces<ProfileDto>();
        g.MapDelete("/cv", (AppDbContext db, IFileStorage storage, CancellationToken ct) =>
            ReplaceAsync(ProfileFile.Cv, null, db, storage, ct)).Produces<ProfileDto>();
    }

    /// <summary>Public: the avatar and CV files, for the About page.</summary>
    public static void MapPublicProfileFiles(this RouteGroupBuilder publicGroup)
    {
        publicGroup.MapGet("/profile/avatar", (AppDbContext db, IFileStorage storage, CancellationToken ct) =>
            StreamAsync(ProfileFile.Avatar, db, storage, ct));
        publicGroup.MapGet("/profile/cv", (AppDbContext db, IFileStorage storage, CancellationToken ct) =>
            StreamAsync(ProfileFile.Cv, db, storage, ct));
    }

    private enum ProfileFile { Avatar, Cv }

    private static readonly Dictionary<string, (string Ext, string Mime)> ImageTypes = new()
    {
        ["jpg"] = (".jpg", "image/jpeg"),
        ["png"] = (".png", "image/png"),
        ["webp"] = (".webp", "image/webp"),
    };

    /// <summary>Recognises the file by its first bytes, never by its name.</summary>
    internal static string? Sniff(ReadOnlySpan<byte> head) =>
        head.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) ? "jpg"
        : head.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ? "png"
        : head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8) ? "webp"
        : head.StartsWith("%PDF-"u8) ? "pdf"
        : null;

    private static async Task<IResult> ReplaceAsync(ProfileFile kind, IFormFile? file, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var p = await db.SiteProfiles.FindAsync([1], ct);
        if (p is null) db.SiteProfiles.Add(p = new SiteProfile());

        string? newKey = null;
        if (file is not null)
        {
            var maxMb = kind == ProfileFile.Avatar ? 5 : 10;
            if (file.Length == 0 || file.Length > maxMb * 1024L * 1024L)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [$"File must be between 1 byte and {maxMb} MB."] });

            await using var stream = file.OpenReadStream();
            var head = new byte[16];
            var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
            var type = Sniff(head.AsSpan(0, read));
            var ok = kind == ProfileFile.Avatar ? type is "jpg" or "png" or "webp" : type == "pdf";
            if (!ok)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = [kind == ProfileFile.Avatar ? "Avatar must be a JPG, PNG or WebP image." : "CV must be a PDF."],
                });

            stream.Position = 0;
            newKey = await storage.SaveAsync(stream, type == "pdf" ? ".pdf" : ImageTypes[type!].Ext, ct);
        }

        var oldKey = kind == ProfileFile.Avatar ? p.AvatarKey : p.CvKey;
        if (kind == ProfileFile.Avatar) p.AvatarKey = newKey;
        else p.CvKey = newKey;
        await db.SaveChangesAsync(ct);
        if (oldKey is not null) await storage.DeleteAsync(oldKey, ct);
        return Results.Ok(ToDto(p));
    }

    private static async Task<IResult> StreamAsync(ProfileFile kind, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var p = await db.SiteProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
        var key = kind == ProfileFile.Avatar ? p?.AvatarKey : p?.CvKey;
        if (key is null || !storage.Exists(key)) return Results.NotFound();
        var ext = Path.GetExtension(key).TrimStart('.');
        var mime = ext == "pdf" ? "application/pdf" : ImageTypes.GetValueOrDefault(ext).Mime ?? "application/octet-stream";
        return Results.Stream(storage.OpenRead(key), mime, kind == ProfileFile.Cv ? "CV.pdf" : null, enableRangeProcessing: true);
    }

    internal static async Task<ProfileDto> GetAsync(AppDbContext db, CancellationToken ct) =>
        ToDto(await db.SiteProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == 1, ct) ?? new SiteProfile());

    /// <summary>File URLs carry the storage file name, so a new upload gets a new URL (no stale caches).</summary>
    private static ProfileDto ToDto(SiteProfile p) => new(
        p.DisplayName, p.HeadlineEn, p.HeadlineVi, p.BioEn, p.BioVi,
        JsonSerializer.Deserialize<Dictionary<string, string>>(p.Links) ?? [],
        p.AvatarKey is null ? null : $"/api/public/profile/avatar?v={Path.GetFileNameWithoutExtension(p.AvatarKey)[..8]}",
        p.CvKey is null ? null : $"/api/public/profile/cv?v={Path.GetFileNameWithoutExtension(p.CvKey)[..8]}");
}
