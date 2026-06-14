using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Data;

/// <summary>
/// Used by EF Core tooling at design time (dotnet ef migrations add).
/// Not used at runtime — runtime context is injected via DI with the real TenantId.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=ep-winter-night-ai0b0p8i.c-4.us-east-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_Zq0Tmk6ditnI;SSL Mode=Require;Trust Server Certificate=true;")
            .Options;

        // Design-time: use Guid.Empty — no tenant filtering during migrations
        return new ApplicationDbContext(options, Guid.Empty);
    }
}
