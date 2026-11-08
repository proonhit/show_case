using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Showcase.Application;

namespace Showcase.Api.Endpoints;

public static class AuthEndpoints
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Owner = new();

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", async (LoginRequest req, IOptions<AuthOptions> options, HttpContext http) =>
        {
            var hash = options.Value.AdminPasswordHash;
            if (string.IsNullOrWhiteSpace(hash))
                return Results.Problem("Admin password is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);

            if (Hasher.VerifyHashedPassword(Owner, hash, req.Password ?? "") == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "owner"), new Claim(AuthOptions.SessionVersionClaim, options.Value.SessionVersion.ToString())],
                CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
            return Results.NoContent();
        })
        .AllowAnonymous()
        .RequireRateLimiting(AuthOptions.LoginRateLimitPolicy);

        auth.MapGet("/me", () => Results.Ok(new { user = "owner" })).RequireAuthorization();

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
