using FluentValidation;

namespace Showcase.Api.Endpoints;

internal static class Validation
{
    /// <summary>Returns a 400 ValidationProblem when the request is invalid, otherwise null.</summary>
    public static async Task<IResult?> ValidateRequestAsync<T>(this IValidator<T> validator, T request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        return result.IsValid ? null : Results.ValidationProblem(result.ToDictionary());
    }
}
