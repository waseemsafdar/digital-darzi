using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public static class DbInitializer
{
    private static readonly string[] DefaultRoles =
    {
        "SystemAdmin",
        "Owner",
        "Manager",
        "Karigar",
        "Receptionist"
    };

    public static async Task SeedAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        // 1. Seed roles (idempotent)
        foreach (var role in DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        // 2. Seed system measurement fields if not already seeded
        var systemFieldExists = await db.MeasurementFields
            .IgnoreQueryFilters()
            .AnyAsync(f => f.TenantId == Guid.Empty, ct);

        if (!systemFieldExists)
        {
            await SeedSystemMeasurementFieldsAsync(db, ct);
        }
    }

    private static async Task SeedSystemMeasurementFieldsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var systemTenantId = Guid.Empty;
        var systemShopId = Guid.Empty;
        var systemUserId = Guid.Empty;

        // 18 system fields
        var fieldDefs = new (string Name, MeasurementUnit Unit, int Sort)[]
        {
            ("Chest",           MeasurementUnit.Inch, 1),
            ("Waist",           MeasurementUnit.Inch, 2),
            ("Hip",             MeasurementUnit.Inch, 3),
            ("Shoulder",        MeasurementUnit.Inch, 4),
            ("Sleeve",          MeasurementUnit.Inch, 5),
            ("Kameez Length",   MeasurementUnit.Inch, 6),
            ("Shirt Length",    MeasurementUnit.Inch, 7),
            ("Collar",          MeasurementUnit.Inch, 8),
            ("Trouser Length",  MeasurementUnit.Inch, 9),
            ("Pant Length",     MeasurementUnit.Inch, 10),
            ("Inseam",          MeasurementUnit.Inch, 11),
            ("Pauncha",         MeasurementUnit.Inch, 12),
            ("Coat Length",     MeasurementUnit.Inch, 13),
            ("Cuff",            MeasurementUnit.Inch, 14),
            ("Back Width",      MeasurementUnit.Inch, 15),
            ("Neck Depth",      MeasurementUnit.Inch, 16),
            ("Bust",            MeasurementUnit.Inch, 17),
            ("Frock Length",    MeasurementUnit.Inch, 18),
        };

        var fields = new Dictionary<string, Guid>();
        var now = DateTime.UtcNow;

        foreach (var (name, unit, sort) in fieldDefs)
        {
            var id = Guid.NewGuid();
            fields[name] = id;
            db.MeasurementFields.Add(new MeasurementField
            {
                Id = id,
                TenantId = systemTenantId,
                BranchId = systemShopId,
                ShopId = systemShopId,
                Name = name,
                Unit = unit,
                SortOrder = sort,
                ActiveStatus = ActiveStatus.Active,
                CreatedBy = systemUserId,
                CreatedOn = now,
                UpdatedBy = systemUserId,
                UpdatedOn = now
            });
        }

        // 7 system templates with their fields
        // ✱ = IsRequired
        var templateDefs = new[]
        {
            new
            {
                Name = "Gents Shalwar Kameez",
                GarmentType = (GarmentType?)GarmentType.ShalwarKameez,
                IsDefault = true,
                Sort = 1,
                Fields = new[] {
                    ("Chest", true), ("Shoulder", true), ("Sleeve", true),
                    ("Kameez Length", true), ("Waist", false), ("Collar", false),
                    ("Trouser Length", true), ("Pauncha", false)
                }
            },
            new
            {
                Name = "Gents Pant Coat",
                GarmentType = (GarmentType?)GarmentType.Coat,
                IsDefault = false,
                Sort = 2,
                Fields = new[] {
                    ("Chest", true), ("Shoulder", true), ("Sleeve", true),
                    ("Coat Length", true), ("Waist", true), ("Hip", false),
                    ("Pant Length", true), ("Inseam", false), ("Cuff", false)
                }
            },
            new
            {
                Name = "Sherwani",
                GarmentType = (GarmentType?)GarmentType.Sherwani,
                IsDefault = false,
                Sort = 3,
                Fields = new[] {
                    ("Chest", true), ("Shoulder", true), ("Sleeve", true),
                    ("Kameez Length", true), ("Waist", false), ("Collar", false),
                    ("Trouser Length", true), ("Pauncha", false), ("Back Width", false)
                }
            },
            new
            {
                Name = "Ladies Shalwar Kameez",
                GarmentType = (GarmentType?)GarmentType.ShalwarKameez,
                IsDefault = false,
                Sort = 4,
                Fields = new[] {
                    ("Chest", true), ("Waist", true), ("Hip", false),
                    ("Shoulder", true), ("Sleeve", true), ("Kameez Length", true),
                    ("Trouser Length", true), ("Neck Depth", false)
                }
            },
            new
            {
                Name = "Ladies Frock",
                GarmentType = (GarmentType?)GarmentType.Frock,
                IsDefault = false,
                Sort = 5,
                Fields = new[] {
                    ("Bust", true), ("Waist", true), ("Hip", true),
                    ("Shoulder", true), ("Sleeve", true), ("Frock Length", true),
                    ("Neck Depth", false)
                }
            },
            new
            {
                Name = "Shirt Only",
                GarmentType = (GarmentType?)GarmentType.Shirt,
                IsDefault = false,
                Sort = 6,
                Fields = new[] {
                    ("Chest", true), ("Shoulder", true), ("Sleeve", true),
                    ("Shirt Length", true), ("Collar", false)
                }
            },
            new
            {
                Name = "Trouser Only",
                GarmentType = (GarmentType?)GarmentType.Trouser,
                IsDefault = false,
                Sort = 7,
                Fields = new[] {
                    ("Waist", true), ("Hip", false), ("Trouser Length", true),
                    ("Inseam", false), ("Pauncha", false)
                }
            }
        };

        foreach (var td in templateDefs)
        {
            var templateId = Guid.NewGuid();
            var template = new MeasurementTemplate
            {
                Id = templateId,
                TenantId = systemTenantId,
                BranchId = systemShopId,
                ShopId = systemShopId,
                Name = td.Name,
                GarmentType = td.GarmentType,
                IsDefault = td.IsDefault,
                IsSystemTemplate = true,
                DisplayOrder = td.Sort,
                ActiveStatus = ActiveStatus.Active,
                CreatedBy = systemUserId,
                CreatedOn = now,
                UpdatedBy = systemUserId,
                UpdatedOn = now
            };

            int sortOrder = 1;
            foreach (var (fieldName, required) in td.Fields)
            {
                if (!fields.TryGetValue(fieldName, out var fieldId)) continue;
                template.TemplateFields.Add(new TemplateField
                {
                    Id = Guid.NewGuid(),
                    TenantId = systemTenantId,
                    BranchId = systemShopId,
                    TemplateId = templateId,
                    MeasurementFieldId = fieldId,
                    IsRequired = required,
                    SortOrder = sortOrder++,
                    CreatedBy = systemUserId,
                    CreatedOn = now
                });
            }

            db.MeasurementTemplates.Add(template);
        }

        await db.SaveChangesAsync(ct);
    }
}
