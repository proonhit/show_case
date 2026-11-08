using Microsoft.EntityFrameworkCore;
using Showcase.Domain;

namespace Showcase.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Model> Models => Set<Model>();
    public DbSet<ModelTranslation> ModelTranslations => Set<ModelTranslation>();
    public DbSet<ModelFile> ModelFiles => Set<ModelFile>();
    public DbSet<ModelStats> ModelStats => Set<ModelStats>();
    public DbSet<ModelImage> ModelImages => Set<ModelImage>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<SiteProfile> SiteProfiles => Set<SiteProfile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Model>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(120);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Software).HasMaxLength(200);
            e.Property(x => x.ViewerSettings).HasColumnType("jsonb");
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Translations).WithOne().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Files).WithOne().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Stats).WithOne().HasForeignKey<ModelStats>(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Tags).WithMany(x => x.Models).UsingEntity(j => j.ToTable("ModelTags"));
        });

        b.Entity<ModelTranslation>(e =>
        {
            e.HasKey(x => new { x.ModelId, x.Locale });
            e.Property(x => x.Locale).HasMaxLength(5);
            e.Property(x => x.Title).HasMaxLength(120);
        });

        b.Entity<ModelFile>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Format).HasMaxLength(8);
            e.Property(x => x.StorageKey).HasMaxLength(260);
        });

        b.Entity<ModelStats>(e =>
        {
            e.HasKey(x => x.ModelId);
            e.Property(x => x.AvailableMaps).HasColumnType("jsonb");
            e.Property(x => x.BoundingBox).HasColumnType("jsonb");
        });

        b.Entity<ModelImage>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.StorageKey).HasMaxLength(260);
        });

        b.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(80);
            e.Property(x => x.NameEn).HasMaxLength(80);
            e.Property(x => x.NameVi).HasMaxLength(80);
        });

        b.Entity<Tag>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(80);
        });

        b.Entity<SiteProfile>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Links).HasColumnType("jsonb");
        });
    }
}
