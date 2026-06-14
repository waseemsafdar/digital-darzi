using Domain.Entities;

namespace Application.Interfaces.Services;

public interface ITenantSetupService
{
    /// <summary>
    /// Called once after a new shop/tenant is created.
    /// Copies all system seed measurement fields and templates (TenantId = Guid.Empty)
    /// into the new tenant's own copies. Runs in a single transaction — all or nothing.
    /// </summary>
    Task SetupNewTenantAsync(Guid tenantId, Guid shopId, Guid createdBy, CancellationToken ct = default);
}
