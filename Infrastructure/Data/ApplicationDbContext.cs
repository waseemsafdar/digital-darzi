using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Infrastructure.Data;

/// <summary>
/// Tenant-scoped EF Core context.
///
/// One instance is built per request from the caller's JWT claims (tenantId, shopId, user id).
/// Global query filters restrict every operational table to that tenant + branch and hide
/// soft-deleted rows. <see cref="SaveChangesAsync(bool, CancellationToken)"/> stamps tenant,
/// branch and audit columns on new rows so no service can forget them, and writes an
/// <see cref="AuditLog"/> row for whitelisted entities.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>
{
    private readonly Guid _tenantId;
    private readonly Guid _shopId;
    private readonly Guid _userId;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, Guid tenantId, Guid shopId = default, Guid userId = default)
        : base(options)
    {
        _tenantId = tenantId;
        _shopId   = shopId;
        _userId   = userId;
    }

    public Guid CurrentTenantId => _tenantId;
    public Guid CurrentShopId   => _shopId;
    public Guid CurrentUserId   => _userId;

    /// <summary>Set by callers that must suppress audit rows (e.g. bulk seeding).</summary>
    public bool AuditEnabled { get; set; } = true;

    /// <summary>Supplies "now" for stamping. Replaced in tests.</summary>
    public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    // ── Domain tables ──────────────────────────────────────────────────────
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Domain.Entities.User> AppUsers => Set<Domain.Entities.User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<MeasurementField> MeasurementFields => Set<MeasurementField>();
    public DbSet<MeasurementTemplate> MeasurementTemplates => Set<MeasurementTemplate>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<MeasurementProfile> MeasurementProfiles => Set<MeasurementProfile>();
    public DbSet<MeasurementProfileVersion> MeasurementProfileVersions => Set<MeasurementProfileVersion>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemStageAssignment> OrderItemStageAssignments => Set<OrderItemStageAssignment>();
    public DbSet<OrderItemStageLog> OrderItemStageLogs => Set<OrderItemStageLog>();
    public DbSet<OrderAlteration> OrderAlterations => Set<OrderAlteration>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<StaffAttendance> StaffAttendances => Set<StaffAttendance>();
    public DbSet<ShopExpense> ShopExpenses => Set<ShopExpense>();
    public DbSet<StaffSalary> StaffSalaries => Set<StaffSalary>();
    public DbSet<GarmentAttribute> GarmentAttributes => Set<GarmentAttribute>();
    public DbSet<KarigarRate> KarigarRates => Set<KarigarRate>();
    public DbSet<GarmentTypeOption> GarmentTypeOptions => Set<GarmentTypeOption>();
    public DbSet<KarigarLedgerEntry> KarigarLedgerEntries => Set<KarigarLedgerEntry>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<SalaryRule> SalaryRules => Set<SalaryRule>();
    public DbSet<SalaryAdvance> SalaryAdvances => Set<SalaryAdvance>();
    public DbSet<ExpenseCategoryDefinition> ExpenseCategories => Set<ExpenseCategoryDefinition>();

    // ── Platform tables (not tenant-filtered; always query with an explicit predicate) ──
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantInvoice> TenantInvoices => Set<TenantInvoice>();
    public DbSet<TenantRolePermission> TenantRolePermissions => Set<TenantRolePermission>();
    public DbSet<ShopRole> ShopRoles => Set<ShopRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Every branch of the current tenant (bypasses the branch filter, keeps the tenant and
    /// soft-delete filters). Used by Owner-level consolidated reports.
    /// </summary>
    public IQueryable<T> TenantWide<T>() where T : BaseDBModel
        => Set<T>().IgnoreQueryFilters().Where(x => x.TenantId == _tenantId && !x.IsDeleted);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("pg_trgm");

        // ── Global Query Filters ────────────────────────────────────────────
        // Shop + User: TenantId only — Owner must see all branches and staff across the tenant.
        builder.Entity<Shop>().HasQueryFilter(e => e.TenantId == _tenantId && !e.IsDeleted);
        builder.Entity<Domain.Entities.User>().HasQueryFilter(e => e.TenantId == _tenantId && !e.IsDeleted);

        // Operational data: TenantId + BranchId — each branch sees only its own records.
        // BranchId == ShopId (Shop.BranchId = Shop.Id is set at provisioning time).
        BranchFilter<Customer>(builder);
        BranchFilter<MeasurementField>(builder);
        BranchFilter<MeasurementTemplate>(builder);
        BranchFilter<TemplateField>(builder, softDelete: false);
        BranchFilter<MeasurementProfile>(builder);
        BranchFilter<MeasurementProfileVersion>(builder, softDelete: false);
        BranchFilter<Order>(builder);
        BranchFilter<OrderItem>(builder);
        BranchFilter<OrderItemStageAssignment>(builder, softDelete: false);
        BranchFilter<OrderItemStageLog>(builder, softDelete: false);
        BranchFilter<OrderAlteration>(builder);
        BranchFilter<OrderPayment>(builder, softDelete: false);
        BranchFilter<OrderStatusHistory>(builder, softDelete: false);
        BranchFilter<Attachment>(builder);
        BranchFilter<StaffAttendance>(builder);
        BranchFilter<ShopExpense>(builder);
        BranchFilter<StaffSalary>(builder);
        BranchFilter<GarmentAttribute>(builder);
        BranchFilter<KarigarRate>(builder);
        BranchFilter<GarmentTypeOption>(builder);
        BranchFilter<KarigarLedgerEntry>(builder);
        BranchFilter<NotificationTemplate>(builder);
        BranchFilter<NotificationMessage>(builder);
        BranchFilter<Supplier>(builder);
        BranchFilter<InventoryItem>(builder);
        BranchFilter<InventoryMovement>(builder, softDelete: false);
        BranchFilter<CashSession>(builder);
        BranchFilter<CashMovement>(builder, softDelete: false);
        BranchFilter<SalaryRule>(builder);
        BranchFilter<SalaryAdvance>(builder);
        BranchFilter<ExpenseCategoryDefinition>(builder);

        // ── Postgres array types for User.RoleIds / ShopIds ─────────────────
        builder.Entity<Domain.Entities.User>().Property(e => e.RoleIds).HasColumnType("uuid[]");
        builder.Entity<Domain.Entities.User>().Property(e => e.ShopIds).HasColumnType("uuid[]");

        // ── JSONB columns ────────────────────────────────────────────────────
        builder.Entity<MeasurementProfile>().Property(e => e.FieldValuesJson).HasColumnType("jsonb");
        builder.Entity<MeasurementProfile>().Property(e => e.TextValuesJson).HasColumnType("jsonb");
        builder.Entity<MeasurementProfileVersion>().Property(e => e.FieldValuesJson).HasColumnType("jsonb");
        builder.Entity<MeasurementProfileVersion>().Property(e => e.TextValuesJson).HasColumnType("jsonb");
        builder.Entity<OrderItem>().Property(e => e.MeasurementSnapshot).HasColumnType("jsonb");
        builder.Entity<OrderItem>().Property(e => e.AttributesJson).HasColumnType("jsonb");
        builder.Entity<GarmentAttribute>().Property(e => e.OptionsJson).HasColumnType("jsonb");
        builder.Entity<AuditLog>().Property(e => e.OldValues).HasColumnType("jsonb");
        builder.Entity<AuditLog>().Property(e => e.NewValues).HasColumnType("jsonb");

        // ── Indexes: existing (kept unchanged so existing constraints keep holding) ──
        builder.Entity<Customer>().HasIndex(e => new { e.TenantId, e.ShopId, e.Phone }).IsUnique();
        builder.Entity<MeasurementField>().HasIndex(e => new { e.TenantId, e.ShopId, e.Name }).IsUnique();
        builder.Entity<TemplateField>().HasIndex(e => new { e.TemplateId, e.MeasurementFieldId }).IsUnique();
        builder.Entity<Order>().HasIndex(e => new { e.TenantId, e.ShopId, e.OrderNumber }).IsUnique();
        builder.Entity<Order>().HasIndex(e => new { e.TenantId, e.CustomerId, e.Status });
        builder.Entity<Order>().HasIndex(e => new { e.TenantId, e.ShopId, e.DeliveryDate });
        builder.Entity<OrderItem>().HasIndex(e => new { e.TenantId, e.OrderId });
        builder.Entity<OrderItemStageAssignment>().HasIndex(e => new { e.OrderItemId, e.Stage }).IsUnique();
        builder.Entity<OrderItemStageAssignment>().HasIndex(e => new { e.TenantId, e.AssignedKarigarId, e.Stage });
        builder.Entity<OrderItemStageLog>().HasIndex(e => new { e.TenantId, e.OrderItemId });
        builder.Entity<OrderItemStageLog>().HasIndex(e => new { e.TenantId, e.KarigarId, e.Stage });
        builder.Entity<OrderAlteration>().HasIndex(e => new { e.TenantId, e.OrderItemId });
        builder.Entity<OrderPayment>().HasIndex(e => new { e.TenantId, e.OrderId });
        builder.Entity<OrderStatusHistory>().HasIndex(e => new { e.TenantId, e.OrderId });
        builder.Entity<StaffAttendance>().HasIndex(e => new { e.TenantId, e.StaffUserId, e.Date }).IsUnique();
        builder.Entity<ShopExpense>().HasIndex(e => new { e.TenantId, e.ShopId, e.ExpenseDate });
        builder.Entity<ShopExpense>().HasIndex(e => new { e.TenantId, e.ShopId, e.Category });
        builder.Entity<StaffSalary>().HasIndex(e => new { e.TenantId, e.StaffUserId, e.Month, e.Year }).IsUnique();
        builder.Entity<StaffSalary>().HasIndex(e => new { e.TenantId, e.ShopId, e.Year, e.Month });

        // ── Indexes: the column every global filter actually uses (TenantId, BranchId) ──
        builder.Entity<Customer>().HasIndex(e => new { e.TenantId, e.BranchId, e.CreatedOn });
        builder.Entity<MeasurementField>().HasIndex(e => new { e.TenantId, e.BranchId });
        builder.Entity<MeasurementTemplate>().HasIndex(e => new { e.TenantId, e.BranchId });
        builder.Entity<MeasurementProfile>().HasIndex(e => new { e.TenantId, e.BranchId, e.CustomerId });
        builder.Entity<Order>().HasIndex(e => new { e.TenantId, e.BranchId, e.DeliveryDate });
        builder.Entity<Order>().HasIndex(e => new { e.TenantId, e.BranchId, e.Status });
        builder.Entity<Order>().HasIndex(e => new { e.TenantId, e.BranchId, e.CreatedOn });
        builder.Entity<OrderItem>().HasIndex(e => new { e.TenantId, e.BranchId });
        builder.Entity<OrderItemStageAssignment>().HasIndex(e => new { e.TenantId, e.BranchId, e.Stage });
        builder.Entity<OrderItemStageLog>().HasIndex(e => new { e.TenantId, e.BranchId, e.CompletedAt });
        builder.Entity<OrderPayment>().HasIndex(e => new { e.TenantId, e.BranchId, e.PaidAt });
        builder.Entity<OrderAlteration>().HasIndex(e => new { e.TenantId, e.BranchId, e.Status });
        builder.Entity<StaffAttendance>().HasIndex(e => new { e.TenantId, e.BranchId, e.Date });
        builder.Entity<ShopExpense>().HasIndex(e => new { e.TenantId, e.BranchId, e.ExpenseDate });
        builder.Entity<StaffSalary>().HasIndex(e => new { e.TenantId, e.BranchId, e.Year, e.Month });
        builder.Entity<Attachment>().HasIndex(e => new { e.TenantId, e.BranchId, e.AttachmentType, e.EntityId });
        builder.Entity<Shop>().HasIndex(e => e.TenantId);
        builder.Entity<Domain.Entities.User>().HasIndex(e => e.TenantId);
        builder.Entity<Domain.Entities.User>().HasIndex(e => e.Phone);

        // ── Trigram indexes for server-side ILIKE search ────────────────────
        builder.Entity<Customer>().HasIndex(e => e.Name).HasMethod("gin").HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_Customers_Name_trgm");
        builder.Entity<Customer>().HasIndex(e => e.Phone).HasMethod("gin").HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_Customers_Phone_trgm");
        builder.Entity<Order>().HasIndex(e => e.OrderNumber).HasMethod("gin").HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_Orders_OrderNumber_trgm");

        // ── Indexes for new tables ───────────────────────────────────────────
        builder.Entity<MeasurementProfileVersion>().HasIndex(e => new { e.ProfileId, e.VersionNo }).IsUnique();
        builder.Entity<GarmentAttribute>().HasIndex(e => new { e.TenantId, e.BranchId, e.GarmentType, e.Name }).IsUnique()
            .AreNullsDistinct(false);
        // One row per code per branch, deleted or not: a deleted custom code is never reused.
        builder.Entity<GarmentTypeOption>().HasIndex(e => new { e.TenantId, e.BranchId, e.Code }).IsUnique();
        builder.Entity<GarmentTypeOption>().Property(e => e.Name).HasMaxLength(60);
        builder.Entity<KarigarRate>().HasIndex(e => new { e.TenantId, e.BranchId, e.KarigarId, e.GarmentType, e.Stage }).IsUnique()
            .AreNullsDistinct(false);
        builder.Entity<KarigarLedgerEntry>().HasIndex(e => new { e.TenantId, e.BranchId, e.KarigarId, e.EntryDate });
        builder.Entity<KarigarLedgerEntry>().HasIndex(e => e.StageLogId).IsUnique()
            .HasFilter("\"StageLogId\" IS NOT NULL AND \"EntryType\" = 1");
        builder.Entity<NotificationTemplate>().HasIndex(e => new { e.TenantId, e.BranchId, e.EventType, e.Channel }).IsUnique();
        builder.Entity<NotificationMessage>().HasIndex(e => new { e.Status, e.ScheduledAt });
        builder.Entity<NotificationMessage>().HasIndex(e => new { e.TenantId, e.BranchId, e.CreatedOn });
        builder.Entity<NotificationMessage>().HasIndex(e => new { e.OrderId, e.EventType });
        builder.Entity<InventoryItem>().HasIndex(e => new { e.TenantId, e.BranchId, e.Sku }).IsUnique()
            .HasFilter("\"Sku\" IS NOT NULL AND \"IsDeleted\" = FALSE");
        builder.Entity<InventoryItem>().HasIndex(e => new { e.TenantId, e.BranchId, e.Category });
        builder.Entity<InventoryMovement>().HasIndex(e => new { e.TenantId, e.BranchId, e.ItemId, e.MovementDate });
        builder.Entity<InventoryMovement>().HasIndex(e => new { e.TenantId, e.OrderItemId });
        builder.Entity<CashSession>().HasIndex(e => new { e.TenantId, e.BranchId }).IsUnique()
            .HasFilter("\"Status\" = 1 AND \"IsDeleted\" = FALSE")
            .HasDatabaseName("IX_CashSessions_OneOpenPerBranch");
        builder.Entity<CashMovement>().HasIndex(e => new { e.TenantId, e.SessionId });
        builder.Entity<SalaryRule>().HasIndex(e => new { e.TenantId, e.BranchId, e.StaffUserId }).IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");
        builder.Entity<SalaryAdvance>().HasIndex(e => new { e.TenantId, e.BranchId, e.StaffUserId });
        builder.Entity<ExpenseCategoryDefinition>().HasIndex(e => new { e.TenantId, e.BranchId, e.Name }).IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");
        builder.Entity<Supplier>().HasIndex(e => new { e.TenantId, e.BranchId, e.Name });

        builder.Entity<RefreshToken>().HasIndex(e => e.TokenHash).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(e => e.UserId);
        builder.Entity<PasswordResetCode>().HasIndex(e => e.UserId);
        builder.Entity<SubscriptionPlan>().HasIndex(e => e.Name).IsUnique();
        builder.Entity<TenantInvoice>().HasIndex(e => e.InvoiceNumber).IsUnique();
        builder.Entity<TenantInvoice>().HasIndex(e => new { e.TenantId, e.PeriodStart });
        builder.Entity<TenantRolePermission>().HasIndex(e => new { e.TenantId, e.RoleName, e.Permission }).IsUnique();
        builder.Entity<ShopRole>().HasIndex(e => new { e.TenantId, e.RoleName }).IsUnique();
        builder.Entity<ShopRole>().Property(e => e.RoleName).HasMaxLength(64);
        builder.Entity<ShopRole>().Property(e => e.DisplayName).HasMaxLength(60);
        builder.Entity<ShopRole>().Property(e => e.Description).HasMaxLength(300);
        builder.Entity<AuditLog>().HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
        builder.Entity<AuditLog>().HasIndex(e => new { e.TenantId, e.CreatedOn });

        // ── Relationships ────────────────────────────────────────────────────
        builder.Entity<Order>().HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId);
        builder.Entity<Order>().HasMany(o => o.Payments).WithOne(p => p.Order).HasForeignKey(p => p.OrderId);
        builder.Entity<Order>().HasMany(o => o.Alterations).WithOne(a => a.Order).HasForeignKey(a => a.OrderId);
        builder.Entity<Order>().HasMany(o => o.StatusHistory).WithOne(h => h.Order).HasForeignKey(h => h.OrderId);
        // Order.Attachments keeps the historical shadow FK column "OrderId" on the Attachment table.
        builder.Entity<Order>().HasMany(o => o.Attachments).WithOne().HasForeignKey("OrderId").IsRequired(false);

        builder.Entity<OrderItem>().HasMany(i => i.StageAssignments).WithOne(a => a.OrderItem).HasForeignKey(a => a.OrderItemId);
        builder.Entity<OrderItem>().HasMany(i => i.StageLogs).WithOne(l => l.OrderItem).HasForeignKey(l => l.OrderItemId);
        builder.Entity<OrderItem>().HasMany(i => i.Alterations).WithOne(a => a.OrderItem).HasForeignKey(a => a.OrderItemId);

        builder.Entity<OrderItemStageAssignment>()
            .HasOne(a => a.AssignedKarigar).WithMany().HasForeignKey(a => a.AssignedKarigarId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<OrderItemStageLog>()
            .HasOne(l => l.Karigar).WithMany().HasForeignKey(l => l.KarigarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MeasurementProfile>().HasMany(p => p.Versions).WithOne(v => v.Profile).HasForeignKey(v => v.ProfileId);

        builder.Entity<ShopExpense>()
            .HasOne(e => e.AddedByUser).WithMany().HasForeignKey(e => e.AddedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<StaffAttendance>()
            .HasOne(a => a.StaffUser).WithMany().HasForeignKey(a => a.StaffUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<StaffSalary>()
            .HasOne(s => s.StaffUser).WithMany().HasForeignKey(s => s.StaffUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InventoryItem>()
            .HasOne(i => i.Supplier).WithMany().HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InventoryMovement>()
            .HasOne(m => m.Item).WithMany().HasForeignKey(m => m.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void BranchFilter<T>(ModelBuilder builder, bool softDelete = true) where T : BaseDBModel
    {
        if (softDelete)
            builder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        else
            builder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId);
    }

    // ── Save pipeline: stamping + audit ────────────────────────────────────

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var audits = BeforeSave();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        if (audits.Count > 0)
        {
            AuditLogs.AddRange(audits);
            base.SaveChanges(acceptAllChangesOnSuccess);
        }
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var audits = BeforeSave();
        if (audits.Count > 0)
            AuditLogs.AddRange(audits);   // same round-trip → audit commits or rolls back with the change
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static readonly HashSet<Type> AuditedTypes = new()
    {
        typeof(Order), typeof(OrderItem), typeof(OrderPayment), typeof(OrderAlteration),
        typeof(OrderItemStageAssignment), typeof(MeasurementProfile), typeof(Customer),
        typeof(Domain.Entities.User), typeof(ShopExpense), typeof(StaffSalary), typeof(Shop),
        typeof(KarigarLedgerEntry), typeof(KarigarRate), typeof(InventoryMovement),
        typeof(CashSession), typeof(SalaryAdvance), typeof(GarmentTypeOption)
    };

    /// <summary>Columns never written to the audit trail (noise or secrets).</summary>
    private static readonly HashSet<string> AuditIgnoredProperties = new(StringComparer.Ordinal)
    {
        nameof(BaseDBModel.UpdatedOn), nameof(BaseDBModel.UpdatedBy), nameof(BaseDBModel.CreatedOn),
        nameof(BaseDBModel.CreatedBy), nameof(BaseDBModel.Sr), nameof(BaseDBModel.ClientId),
        "Version", "Passcode", "LastLoginDate", "LoginCount", "FailedLoginCount", "LockoutEndsAt",
        "MeasurementSnapshot"
    };

    private List<AuditLog> BeforeSave()
    {
        var now = UtcNow();
        var audits = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries<BaseDBModel>().ToList())
        {
            var entity = entry.Entity;
            switch (entry.State)
            {
                case EntityState.Added:
                    StampNew(entity, now);
                    break;
                case EntityState.Modified:
                    entry.Property(nameof(BaseDBModel.UpdatedOn)).CurrentValue = now;
                    if (_userId != Guid.Empty) entity.UpdatedBy = _userId;
                    // Tenant/branch of an existing row can never be moved by an update.
                    entry.Property(nameof(BaseDBModel.TenantId)).IsModified = false;
                    if (entity is not Shop && entity is not Domain.Entities.User)
                        entry.Property(nameof(BaseDBModel.BranchId)).IsModified = false;
                    break;
            }

            if (AuditEnabled && AuditedTypes.Contains(entry.Metadata.ClrType)
                && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var log = BuildAudit(entry, now);
                if (log != null) audits.Add(log);
            }
        }
        return audits;
    }

    private void StampNew(BaseDBModel entity, DateTime now)
    {
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (entity.TenantId == Guid.Empty && _tenantId != Guid.Empty) entity.TenantId = _tenantId;

        // Shop and User rows choose their own BranchId (Shop.BranchId = Shop.Id; User = home branch).
        if (entity is not Shop && entity is not Domain.Entities.User
            && entity.BranchId == Guid.Empty && _shopId != Guid.Empty)
            entity.BranchId = _shopId;

        if (entity is IShopScoped scoped)
        {
            if (scoped.ShopId == Guid.Empty) scoped.ShopId = entity.BranchId;
            if (entity.BranchId == Guid.Empty) entity.BranchId = scoped.ShopId;
        }

        if (entity.CreatedBy == Guid.Empty && _userId != Guid.Empty) entity.CreatedBy = _userId;
        if (entity.UpdatedBy == Guid.Empty) entity.UpdatedBy = entity.CreatedBy;
        if (entity.CreatedOn == default) entity.CreatedOn = now;
    }

    private AuditLog? BuildAudit(EntityEntry<BaseDBModel> entry, DateTime now)
    {
        Dictionary<string, object?>? oldValues = null;
        Dictionary<string, object?>? newValues = null;

        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (AuditIgnoredProperties.Contains(name) || prop.Metadata.IsShadowProperty()) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    (newValues ??= new())[name] = prop.CurrentValue;
                    break;
                case EntityState.Deleted:
                    (oldValues ??= new())[name] = prop.OriginalValue;
                    break;
                case EntityState.Modified when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue):
                    (oldValues ??= new())[name] = prop.OriginalValue;
                    (newValues ??= new())[name] = prop.CurrentValue;
                    break;
            }
        }

        if (entry.State == EntityState.Modified && newValues == null) return null;   // nothing really changed

        var action = entry.State switch
        {
            EntityState.Added   => "Created",
            EntityState.Deleted => "Deleted",
            _ => newValues != null && newValues.ContainsKey(nameof(BaseDBModel.IsDeleted))
                     && Equals(newValues[nameof(BaseDBModel.IsDeleted)], true) ? "Deleted" : "Updated"
        };

        return new AuditLog
        {
            Id         = Guid.NewGuid(),
            TenantId   = entry.Entity.TenantId != Guid.Empty ? entry.Entity.TenantId : _tenantId,
            BranchId   = entry.Entity.BranchId != Guid.Empty ? entry.Entity.BranchId : _shopId,
            UserId     = _userId,
            Action     = action,
            EntityType = entry.Metadata.ClrType.Name,
            EntityId   = entry.Entity.Id,
            OldValues  = oldValues == null ? null : JsonSerializer.Serialize(oldValues),
            NewValues  = newValues == null ? null : JsonSerializer.Serialize(newValues),
            CreatedOn  = now
        };
    }
}
