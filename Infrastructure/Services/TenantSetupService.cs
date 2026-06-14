using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class TenantSetupService : ITenantSetupService
{
    private readonly ApplicationDbContext _systemContext;   // context with NO tenant filter (uses Guid.Empty)

    public TenantSetupService(ApplicationDbContext systemContext)
    {
        _systemContext = systemContext;
    }

    public async Task SetupNewTenantAsync(Guid tenantId, Guid shopId, Guid createdBy, CancellationToken ct = default)
    {
        await using var tx = await _systemContext.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Load system seed fields (TenantId = Guid.Empty)
            var systemFields = await _systemContext.MeasurementFields
                .IgnoreQueryFilters()
                .Where(f => f.TenantId == Guid.Empty && !f.IsDeleted)
                .ToListAsync(ct);

            // 2. Copy fields → build old-guid → new-guid map for template wiring
            var fieldIdMap = new Dictionary<Guid, Guid>();
            var newFields = new List<MeasurementField>();
            foreach (var sf in systemFields)
            {
                var newId = Guid.NewGuid();
                fieldIdMap[sf.Id] = newId;
                newFields.Add(new MeasurementField
                {
                    Id = newId,
                    TenantId = tenantId,
                    BranchId = shopId,
                    ShopId = shopId,
                    Name = sf.Name,
                    Unit = sf.Unit,
                    IsRequired = sf.IsRequired,
                    SortOrder = sf.SortOrder,
                    ActiveStatus = ActiveStatus.Active,
                    CreatedBy = createdBy,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedBy = createdBy,
                    UpdatedOn = DateTime.UtcNow
                });
            }
            _systemContext.MeasurementFields.AddRange(newFields);

            // 3. Load system templates + their fields
            var systemTemplates = await _systemContext.MeasurementTemplates
                .IgnoreQueryFilters()
                .Include(t => t.TemplateFields)
                .Where(t => t.TenantId == Guid.Empty && !t.IsDeleted)
                .ToListAsync(ct);

            // 4. Copy templates + remap field IDs
            foreach (var st in systemTemplates)
            {
                var newTemplateId = Guid.NewGuid();
                var newTemplate = new MeasurementTemplate
                {
                    Id = newTemplateId,
                    TenantId = tenantId,
                    BranchId = shopId,
                    ShopId = shopId,
                    Name = st.Name,
                    Description = st.Description,
                    GarmentType = st.GarmentType,
                    IsDefault = st.IsDefault,
                    IsSystemTemplate = false,    // owned by tenant now
                    DisplayOrder = st.DisplayOrder,
                    ActiveStatus = ActiveStatus.Active,
                    CreatedBy = createdBy,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedBy = createdBy,
                    UpdatedOn = DateTime.UtcNow
                };

                foreach (var stf in st.TemplateFields)
                {
                    if (!fieldIdMap.TryGetValue(stf.MeasurementFieldId, out var newFieldId))
                        continue;   // skip if field wasn't copied (shouldn't happen)

                    newTemplate.TemplateFields.Add(new TemplateField
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        BranchId = shopId,
                        TemplateId = newTemplateId,
                        MeasurementFieldId = newFieldId,
                        IsRequired = stf.IsRequired,
                        SortOrder = stf.SortOrder,
                        CreatedBy = createdBy,
                        CreatedOn = DateTime.UtcNow
                    });
                }

                _systemContext.MeasurementTemplates.Add(newTemplate);
            }

            await _systemContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
