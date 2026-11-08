using FluentValidation;

namespace Showcase.Application;

internal static class Rules
{
    public const string SlugPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
    public static readonly string[] Locales = ["en", "vi"];
    public static readonly string[] Statuses = ["Draft", "Published", "Archived"];
}

public class UpsertCategoryValidator : AbstractValidator<UpsertCategoryRequest>
{
    public UpsertCategoryValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(80);
        RuleFor(x => x.NameVi).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(80).Matches(Rules.SlugPattern)
            .WithMessage("Slug must be lowercase letters, digits and single hyphens.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateModelValidator : AbstractValidator<CreateModelRequest>
{
    public CreateModelValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(120).Matches(Rules.SlugPattern)
            .WithMessage("Slug must be lowercase letters, digits and single hyphens.");
        RuleFor(x => x.Software).MaximumLength(200);
        RuleFor(x => x.HoursSpent).GreaterThanOrEqualTo(0).When(x => x.HoursSpent.HasValue);
        RuleFor(x => x.Translations).NotEmpty()
            .Must(t => t.Any(x => x.Locale == "en")).WithMessage("An English (en) translation is required.")
            .Must(t => t.Select(x => x.Locale).Distinct().Count() == t.Count).WithMessage("Each locale may appear once.");
        RuleForEach(x => x.Translations).ChildRules(t =>
        {
            t.RuleFor(x => x.Locale).Must(l => Rules.Locales.Contains(l)).WithMessage("Locale must be 'en' or 'vi'.");
            t.RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
            t.RuleFor(x => x.Description).MaximumLength(5000);
        });
    }
}

public class UpdateModelStatusValidator : AbstractValidator<UpdateModelStatusRequest>
{
    public UpdateModelStatusValidator()
    {
        RuleFor(x => x.Status).Must(s => Rules.Statuses.Contains(s))
            .WithMessage("Status must be Draft, Published or Archived.");
    }
}
