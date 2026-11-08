namespace Showcase.Application;

public record LoginRequest(string Password);

public record CategoryDto(Guid Id, string NameEn, string NameVi, string Slug, int SortOrder);

public record UpsertCategoryRequest(string NameEn, string NameVi, string Slug, int SortOrder);

public record TranslationDto(string Locale, string Title, string? Description);

/// <summary>Body for creating (POST) and editing (PUT) a model.</summary>
public record UpsertModelRequest(
    string Slug,
    Guid? CategoryId,
    string? Software,
    int? HoursSpent,
    DateOnly? CompletedAt,
    List<TranslationDto> Translations,
    List<Guid>? TagIds = null);

public record UpdateModelStatusRequest(string Status, bool IsFeatured);

public record ModelSummaryDto(
    Guid Id,
    string Slug,
    string Status,
    bool IsFeatured,
    int SortOrder,
    string? CategorySlug,
    IReadOnlyList<TranslationDto> Translations,
    int? Triangles,
    IReadOnlyList<string> TagSlugs,
    DateTimeOffset UpdatedAt,
    string? ThumbnailUrl = null);

/// <summary>Everything the Studio edit form needs.</summary>
public record ModelDetailDto(
    Guid Id,
    string Slug,
    string Status,
    bool IsFeatured,
    int SortOrder,
    Guid? CategoryId,
    string? Software,
    int? HoursSpent,
    DateOnly? CompletedAt,
    IReadOnlyList<TranslationDto> Translations,
    IReadOnlyList<Guid> TagIds,
    DateTimeOffset UpdatedAt,
    ViewerSettingsDto ViewerSettings,
    IReadOnlyList<ModelImageDto> Images);

/// <summary>
/// How the viewer opens this model. Camera position/target are both set (a saved view) or both null (auto framing).
/// <c>Background</c> null = the site's theme colour.
/// </summary>
public record ViewerSettingsDto(
    bool AutoRotate = true,
    double AutoRotateSpeed = 20,
    string? Background = null,
    string DefaultMode = "final",
    double[]? CameraPosition = null,
    double[]? CameraTarget = null);

/// <summary>Thumbnail (one per model), renders, wireframes and process shots.</summary>
public record ModelImageDto(Guid Id, string Kind, string Url, string? CaptionEn, string? CaptionVi, int SortOrder);

public record UpdateImageRequest(string? CaptionEn, string? CaptionVi);

public record ReorderRequest(List<Guid> Ids);

public record TagDto(Guid Id, string NameEn, string NameVi, string Slug);

public record UpsertTagRequest(string NameEn, string NameVi, string Slug);

/// <summary>
/// About page content. <c>Links</c> maps a name (linkedin, artstation, email…) to a URL or address.
/// <c>AvatarUrl</c> and <c>CvUrl</c> are filled in on responses and ignored on requests (files have their own endpoints).
/// </summary>
public record ProfileDto(
    string DisplayName,
    string? HeadlineEn,
    string? HeadlineVi,
    string? BioEn,
    string? BioVi,
    Dictionary<string, string> Links,
    string? AvatarUrl = null,
    string? CvUrl = null);

public record MeDto(string User);

public record ReorderModelsRequest(List<Guid> Ids);

/// <summary>Dashboard numbers. StorageBytes counts every stored file version.</summary>
public record StudioStatsDto(int Total, int Published, int Draft, int Archived, long StorageBytes);
