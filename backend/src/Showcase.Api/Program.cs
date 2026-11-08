using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Showcase.Api;
using Showcase.Api.Endpoints;
using Showcase.Application;
using Showcase.Infrastructure;

// `dotnet run --project src/Showcase.Api -- hash-password <password>` prints the value for Auth__AdminPasswordHash.
if (args is ["hash-password", var plain])
{
    Console.WriteLine(new PasswordHasher<object>().HashPassword(new object(), plain));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Upload"));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<UpsertCategoryValidator>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var production = builder.Environment.IsProduction();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "sc_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = production ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        // An API answers 401/403 instead of redirecting to a login page.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        // Bumping Auth:SessionVersion logs out every existing session.
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var current = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.SessionVersion;
            if (ctx.Principal?.FindFirstValue(AuthOptions.SessionVersionClaim) != current.ToString())
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthOptions.LoginRateLimitPolicy, http =>
    {
        var permit = http.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.LoginPermitPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
});

var app = builder.Build();

// Apply EF migrations on start (dev, tests, and the single-server production deploy).
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapCategoryEndpoints();
app.MapModelEndpoints();
app.MapPublicEndpoints();
app.MapUploadEndpoints();

app.Run();

public partial class Program;
