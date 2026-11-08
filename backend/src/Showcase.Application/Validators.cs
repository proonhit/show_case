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

public class UpsertModelValidator : AbstractValidator<UpsertModelRequest>
{
    public UpsertModelValidator()
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
        RuleFor(x => x.TagIds).Must(t => t!.Count <= 20).WithMessage("At most 20 tags.").When(x => x.TagIds is not null);
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

public class UpsertTagValidator : AbstractValidator<UpsertTagRequest>
{
    public UpsertTagValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(60);
        RuleFor(x => x.NameVi).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(60).Matches(Rules.SlugPattern)
            .WithMessage("Slug must be lowercase letters, digits and single hyphens.");
    }
}

public class ProfileValidator : AbstractValidator<ProfileDto>
{
    public ProfileValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.HeadlineEn).MaximumLength(160);
        RuleFor(x => x.HeadlineVi).MaximumLength(160);
        RuleFor(x => x.BioEn).MaximumLength(5000);
        RuleFor(x => x.BioVi).MaximumLength(5000);
        RuleFor(x => x.Links).NotNull().Must(l => l is null || l.Count <= 12).WithMessage("At most 12 links.");
        RuleForEach(x => x.Links).ChildRules(l =>
        {
            l.RuleFor(x => x.Key).NotEmpty().MaximumLength(30).Matches("^[a-z0-9-]+$")
                .WithMessage("Link names are lowercase, e.g. 'linkedin'.");
            l.RuleFor(x => x.Value).NotEmpty().MaximumLength(300).Must(BeLink)
                .WithMessage("Link must be an http(s) URL or an email address.");
        });
    }

    private static bool BeLink(string value) =>
        (Uri.TryCreate(value, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
        || System.Net.Mail.MailAddress.TryCreate(value, out _);
}
