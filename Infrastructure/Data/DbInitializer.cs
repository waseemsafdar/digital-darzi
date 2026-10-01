using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    /// <summary>Initial SystemAdmin, read from configuration ("Seed:AdminEmail" / "Seed:AdminPassword").</summary>
    public sealed record AdminSeed(string? Email, string? Password);

    /// <summary>
    /// Idempotent seeding: roles, the system measurement catalogue, default subscription plans,
    /// and — only when configured and no Identity user exists yet — the first SystemAdmin.
    /// There is no built-in password: without configuration no admin is created.
    /// </summary>
    public static async Task SeedAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ITenantSetupService tenantSetup,
        AdminSeed admin,
        Microsoft.Extensions.Logging.ILogger logger,
        CancellationToken ct = default)
    {
        db.AuditEnabled = false;

        foreach (var role in DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        if (!await db.MeasurementFields.IgnoreQueryFilters().AnyAsync(f => f.TenantId == Guid.Empty, ct))
            await SeedSystemMeasurementFieldsAsync(db, ct);

        if (!await db.SubscriptionPlans.AnyAsync(ct))
        {
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlan { Name = "Starter",  MonthlyPrice = 1500, MaxBranches = 1, MaxUsers = 5,  MaxOrdersPerMonth = 300, TrialDays = 14 },
                new SubscriptionPlan { Name = "Growth",   MonthlyPrice = 3500, MaxBranches = 3, MaxUsers = 20, MaxOrdersPerMonth = 2000, TrialDays = 14 },
                new SubscriptionPlan { Name = "Business", MonthlyPrice = 7500, MaxBranches = null, MaxUsers = null, MaxOrdersPerMonth = null, TrialDays = 14 });
            await db.SaveChangesAsync(ct);
        }

        if (await userManager.Users.AnyAsync(ct)) return;

        if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
        {
            logger.LogWarning("No users exist and Seed:AdminEmail / Seed:AdminPassword are not configured; no SystemAdmin was created.");
            return;
        }

        var adminUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = admin.Email, Email = admin.Email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(adminUser, admin.Password);
        if (!result.Succeeded)
        {
            logger.LogError("Seeding the SystemAdmin failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }
        await userManager.AddToRoleAsync(adminUser, "SystemAdmin");

        var systemTenantId = Guid.NewGuid();
        var systemShopId = Guid.NewGuid();
        var sysRole = await roleManager.FindByNameAsync("SystemAdmin");
        var domainUser = new User
        {
            Id           = Guid.NewGuid(),
            TenantId     = systemTenantId,
            BranchId     = systemShopId,
            Name         = "System Admin",
            Email        = admin.Email,
            AuthId       = adminUser.Id,
            RoleIds      = sysRole != null ? new List<Guid> { sysRole.Id } : new List<Guid>(),
            ShopIds      = new List<Guid> { systemShopId },
            ActiveStatus = ActiveStatus.Active
        };
        db.AppUsers.Add(domainUser);
        db.Shops.Add(new Shop
        {
            Id = systemShopId, TenantId = systemTenantId, BranchId = systemShopId, Name = "Digital Darzi HQ",
            ActiveStatus = ActiveStatus.Active, CreatedBy = domainUser.Id, UpdatedBy = domainUser.Id
        });
        await db.SaveChangesAsync(ct);
        await tenantSetup.SetupNewTenantAsync(systemTenantId, systemShopId, domainUser.Id, ct);
        logger.LogInformation("Seeded SystemAdmin {Email}. Change the password after first sign-in.", admin.Email);
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
