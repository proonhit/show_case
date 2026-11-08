using Microsoft.EntityFrameworkCore;
using Showcase.Domain;

namespace Showcase.Infrastructure;

/// <summary>First-run data: starter categories and an empty profile row. Runs only when the tables are empty.</summary>
public static class DbSeeder
{
    public static readonly (string Slug, string En, string Vi)[] Categories =
    [
        ("characters", "Characters", "Nhân vật"),
        ("props", "Props", "Đồ vật"),
        ("environments", "Environments", "Môi trường"),
        ("vehicles", "Vehicles", "Phương tiện"),
        ("weapons", "Weapons", "Vũ khí"),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (!await db.Categories.AnyAsync(ct))
        {
            db.Categories.AddRange(Categories.Select((c, i) =>
                new Category { Slug = c.Slug, NameEn = c.En, NameVi = c.Vi, SortOrder = i }));
        }

        if (!await db.SiteProfiles.AnyAsync(ct))
            db.SiteProfiles.Add(new SiteProfile());

        await db.SaveChangesAsync(ct);
    }
}
