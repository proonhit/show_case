namespace Showcase.Domain;

public enum ModelStatus { Draft, Published, Archived }

public enum FileKind { Original, Optimized, Mobile }

public enum ImageKind { Thumbnail, Render, Wireframe, Process }

public enum JobStatus { Queued, Running, Succeeded, Failed }

/// <summary>A 3D model shown in the portfolio. Text lives in <see cref="Translations"/> (en / vi).</summary>
public class Model
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Slug { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public ModelStatus Status { get; set; } = ModelStatus.Draft;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public string? Software { get; set; }
    public int? HoursSpent { get; set; }
    public DateOnly? CompletedAt { get; set; }

    /// <summary>JSON: default camera, auto-rotate, background, HDRI, default map mode.</summary>
    public string ViewerSettings { get; set; } = "{}";

    public int ViewCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }

    public List<ModelTranslation> Translations { get; set; } = [];
    public List<ModelFile> Files { get; set; } = [];
    public ModelStats? Stats { get; set; }
    public List<ModelImage> Images { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
}

public class ModelTranslation
{
    public Guid ModelId { get; set; }
    public required string Locale { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
}

public class ModelFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModelId { get; set; }
    public FileKind Kind { get; set; }
    public required string Format { get; set; }
    public required string StorageKey { get; set; }
    public long SizeBytes { get; set; }
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ModelStats
{
    public Guid ModelId { get; set; }
    public int Triangles { get; set; }
    public int Vertices { get; set; }
    public int Meshes { get; set; }
    public int Materials { get; set; }
    public int Textures { get; set; }
    public int MaxTextureSize { get; set; }
    public bool HasAnimation { get; set; }

    /// <summary>JSON array, e.g. ["baseColor","metallicRoughness","normal","occlusion"].</summary>
    public string AvailableMaps { get; set; } = "[]";

    /// <summary>JSON: {"min":[x,y,z],"max":[x,y,z]}.</summary>
    public string BoundingBox { get; set; } = "{}";
}

public class ModelImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModelId { get; set; }
    public ImageKind Kind { get; set; }
    public required string StorageKey { get; set; }

    /// <summary>Small WebP copy (about 640 px wide) for grids and cards.</summary>
    public string? SmallStorageKey { get; set; }

    public string? CaptionEn { get; set; }
    public string? CaptionVi { get; set; }
    public int SortOrder { get; set; }
}

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string NameEn { get; set; }
    public required string NameVi { get; set; }
    public required string Slug { get; set; }
    public int SortOrder { get; set; }
}

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string NameEn { get; set; }
    public required string NameVi { get; set; }
    public required string Slug { get; set; }
    public List<Model> Models { get; set; } = [];
}

/// <summary>Single row (Id = 1) holding the About page content.</summary>
public class SiteProfile
{
    public int Id { get; set; } = 1;
    public string DisplayName { get; set; } = "";
    public string? HeadlineEn { get; set; }
    public string? HeadlineVi { get; set; }
    public string? BioEn { get; set; }
    public string? BioVi { get; set; }
    public string? AvatarKey { get; set; }
    public string? CvKey { get; set; }

    /// <summary>JSON object: {"linkedin":"…","email":"…"}.</summary>
    public string Links { get; set; } = "{}";
}

/// <summary>
/// One run of the processing pipeline for an uploaded file. <see cref="Steps"/> is a JSON array of
/// {name, status, durationMs, message} so the Studio can show each step as it happens.
/// </summary>
public class ProcessingJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModelId { get; set; }
    public Guid ModelFileId { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string Steps { get; set; } = "[]";
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}
