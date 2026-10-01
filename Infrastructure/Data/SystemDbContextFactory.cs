using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

/// <summary>
/// Creates contexts with no tenant scope for platform work that runs outside a request:
/// migrations, seeding, and background jobs. Every query made through such a context must
/// add its own tenant predicate (the global filters match nothing when the tenant is empty).
/// </summary>
public interface ISystemDbContextFactory
{
    ApplicationDbContext Create();

    /// <summary>A context scoped to one tenant/branch, for background work on that branch's data.</summary>
    ApplicationDbContext CreateForBranch(Guid tenantId, Guid shopId);
}

public sealed class SystemDbContextFactory : ISystemDbContextFactory
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public SystemDbContextFactory(DbContextOptions<ApplicationDbContext> options) => _options = options;

    public ApplicationDbContext Create() => new(_options, Guid.Empty, Guid.Empty, Guid.Empty);

    public ApplicationDbContext CreateForBranch(Guid tenantId, Guid shopId) => new(_options, tenantId, shopId, Guid.Empty);
}
