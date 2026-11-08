namespace Showcase.Application;

public record LoginRequest(string Password);

public record CategoryDto(Guid Id, string NameEn, string NameVi, string Slug, int SortOrder);

public record UpsertCategoryRequest(string NameEn, string NameVi, string Slug, int SortOrder);

public record TranslationDto(string Locale, string Title, string? Description);

public record CreateModelRequest(
    string Slug,
    Guid? CategoryId,
    string? Software,
    int? HoursSpent,
    DateOnly? CompletedAt,
    List<TranslationDto> Translations);

public record UpdateModelStatusRequest(string Status, bool IsFeatured);

public record ModelSummaryDto(
    Guid Id,
    string Slug,
    string Status,
    bool IsFeatured,
    int SortOrder,
    string? CategorySlug,
    IReadOnlyList<TranslationDto> Translations,
    int? Triangles);
