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
    IReadOnlyList<string> TagSlugs);

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
    DateTimeOffset UpdatedAt);

public record TagDto(Guid Id, string NameEn, string NameVi, string Slug);

public record UpsertTagRequest(string NameEn, string NameVi, string Slug);

/// <summary>About page content. <c>Links</c> maps a name (linkedin, artstation, email…) to a URL or address.</summary>
public record ProfileDto(
    string DisplayName,
    string? HeadlineEn,
    string? HeadlineVi,
    string? BioEn,
    string? BioVi,
    Dictionary<string, string> Links);
