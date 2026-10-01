using System.Text.Json;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Seeds a new branch with its own copy of the system measurement catalogue, a starter set of
/// garment style attributes and the default expense categories. Idempotent: each part is skipped
/// when the branch already has data of that kind, so it can also back-fill existing branches.
/// Does NOT manage its own transaction — callers wrap it together with the branch creation.
/// </summary>
public class TenantSetupService : ITenantSetupService
{
    private readonly ApplicationDbContext _db;

    public TenantSetupService(ApplicationDbContext db) => _db = db;

    public static readonly string[] DefaultExpenseCategories =
    {
        "Rent", "Electricity", "Salaries", "Karigar Payments", "Supplies", "Transport", "Maintenance", "Miscellaneous"
    };

    private static readonly (GarmentType? Garment, string Name, string[] Options)[] DefaultAttributes =
    {
        (GarmentType.ShalwarKameez, "Collar style", new[] { "Shirt collar", "Band collar", "Sherwani collar", "Round neck" }),
        (GarmentType.ShalwarKameez, "Cuff style",   new[] { "Plain", "Round cuff", "Square cuff", "Kurta cuff" }),
        (GarmentType.ShalwarKameez, "Pockets",      new[] { "None", "Side", "Front", "Side and front" }),
        (GarmentType.ShalwarKameez, "Buttons",      new[] { "Standard", "Fancy", "Hidden placket" }),
        (GarmentType.ShalwarKameez, "Daman",        new[] { "Straight", "Round" }),
        (GarmentType.Kurta,         "Collar style", new[] { "Band collar", "Round neck", "Shirt collar" }),
        (GarmentType.Shirt,         "Collar style", new[] { "Classic", "Button-down", "Mandarin" }),
        (GarmentType.Shirt,         "Cuff style",   new[] { "Single", "Double (French)" }),
        (GarmentType.Waistcoat,     "Buttons",      new[] { "4 buttons", "5 buttons", "6 buttons" }),
        (GarmentType.Suit,          "Lapel",        new[] { "Notch", "Peak", "Shawl" }),
        (GarmentType.Suit,          "Vents",        new[] { "None", "Single", "Double" }),
    };

    public async Task SetupNewTenantAsync(Guid tenantId, Guid shopId, Guid createdBy, CancellationToken ct = default)
    {
        await SeedMeasurementsAsync(tenantId, shopId, createdBy, ct);
        await SeedGarmentAttributesAsync(tenantId, shopId, createdBy, ct);
        await SeedExpenseCategoriesAsync(tenantId, shopId, createdBy, ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedMeasurementsAsync(Guid tenantId, Guid shopId, Guid createdBy, CancellationToken ct)
    {
        var hasFields = await _db.MeasurementFields.IgnoreQueryFilters()
            .AnyAsync(f => f.TenantId == tenantId && f.BranchId == shopId && !f.IsDeleted, ct);
        if (hasFields) return;

        var systemFields = await _db.MeasurementFields.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.TenantId == Guid.Empty && !f.IsDeleted)
            .ToListAsync(ct);

        var fieldIdMap = new Dictionary<Guid, Guid>();
        foreach (var sf in systemFields)
        {
            var newId = Guid.NewGuid();
            fieldIdMap[sf.Id] = newId;
            _db.MeasurementFields.Add(new MeasurementField
            {
                Id = newId, TenantId = tenantId, BranchId = shopId, ShopId = shopId,
                Name = sf.Name, Unit = sf.Unit, ValueType = sf.ValueType, IsRequired = sf.IsRequired, SortOrder = sf.SortOrder,
                ActiveStatus = ActiveStatus.Active, CreatedBy = createdBy, UpdatedBy = createdBy
            });
        }

        var systemTemplates = await _db.MeasurementTemplates.IgnoreQueryFilters().AsNoTracking()
            .Include(t => t.TemplateFields)
            .Where(t => t.TenantId == Guid.Empty && !t.IsDeleted)
            .ToListAsync(ct);

        foreach (var st in systemTemplates)
        {
            var template = new MeasurementTemplate
            {
                Id = Guid.NewGuid(), TenantId = tenantId, BranchId = shopId, ShopId = shopId,
                Name = st.Name, Description = st.Description, GarmentType = st.GarmentType, IsDefault = st.IsDefault,
                IsSystemTemplate = false, DisplayOrder = st.DisplayOrder,
                ActiveStatus = ActiveStatus.Active, CreatedBy = createdBy, UpdatedBy = createdBy
            };
            foreach (var stf in st.TemplateFields.Where(tf => fieldIdMap.ContainsKey(tf.MeasurementFieldId)))
            {
                template.TemplateFields.Add(new TemplateField
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, BranchId = shopId, TemplateId = template.Id,
                    MeasurementFieldId = fieldIdMap[stf.MeasurementFieldId], IsRequired = stf.IsRequired,
                    SortOrder = stf.SortOrder, CreatedBy = createdBy
                });
            }
            _db.MeasurementTemplates.Add(template);
        }
    }

    private async Task SeedGarmentAttributesAsync(Guid tenantId, Guid shopId, Guid createdBy, CancellationToken ct)
    {
        var has = await _db.GarmentAttributes.IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenantId && a.BranchId == shopId && !a.IsDeleted, ct);
        if (has) return;

        var order = 1;
        foreach (var (garment, name, options) in DefaultAttributes)
        {
            _db.GarmentAttributes.Add(new GarmentAttribute
            {
                TenantId = tenantId, BranchId = shopId, GarmentType = garment, Name = name,
                OptionsJson = JsonSerializer.Serialize(options), IsRequired = false, SortOrder = order++,
                CreatedBy = createdBy, UpdatedBy = createdBy
            });
        }
    }

    private async Task SeedExpenseCategoriesAsync(Guid tenantId, Guid shopId, Guid createdBy, CancellationToken ct)
    {
        var has = await _db.ExpenseCategories.IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && c.BranchId == shopId && !c.IsDeleted, ct);
        if (has) return;

        for (var i = 0; i < DefaultExpenseCategories.Length; i++)
        {
            _db.ExpenseCategories.Add(new ExpenseCategoryDefinition
            {
                TenantId = tenantId, BranchId = shopId, Name = DefaultExpenseCategories[i], SortOrder = i + 1,
                CreatedBy = createdBy, UpdatedBy = createdBy
            });
        }
    }
}
