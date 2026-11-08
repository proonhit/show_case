using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Showcase.Application;

namespace Showcase.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Read the connection string lazily so test hosts can override configuration.
        services.AddDbContext<AppDbContext>((sp, o) =>
            o.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }
}
