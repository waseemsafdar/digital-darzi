using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Data;

/// <summary>
/// Used only by EF Core tooling (dotnet ef migrations add / database update).
/// Reads the connection string from the environment so no credential lives in source:
///   ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
/// Falls back to a local development database when the variable is not set.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                         ?? "Host=localhost;Port=5432;Database=digitaldarzi;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection)
            .Options;

        // Design-time: no tenant scope.
        return new ApplicationDbContext(options, Guid.Empty);
    }
}
