using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Showcase.Infrastructure;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without starting the API or a database.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=showcase_design;Username=design;Password=design")
            .Options);
}
