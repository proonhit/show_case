using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Showcase.Api.Tests;

/// <summary>Runs the API against a real PostgreSQL started in Docker (Testcontainers).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "test-password-123";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), "showcase-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Database:AutoCreate", "true");
        builder.UseSetting("Storage:Root", _uploads);
        builder.UseSetting("Auth:AdminPasswordHash", new PasswordHasher<object>().HashPassword(new object(), Password));
        builder.UseSetting("Auth:LoginPermitPerMinute", "1000");
    }

    /// <summary>A client that keeps cookies, already logged in as the owner.</summary>
    public async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var res = await client.PostAsJsonAsync("/api/auth/login", new { password = Password });
        res.EnsureSuccessStatusCode();
        return client;
    }

    public Task InitializeAsync() => _db.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
