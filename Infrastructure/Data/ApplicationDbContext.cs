using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>
{
    private readonly Guid _tenantId;
    private readonly Guid _shopId;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, Guid tenantId, Guid shopId = default)
        : base(options)
    {
        _tenantId = tenantId;
        _shopId   = shopId;
    }

    // ── Domain tables ──────────────────────────────────────────────────────
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Domain.Entities.User> AppUsers => Set<Domain.Entities.User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<MeasurementField> MeasurementFields => Set<MeasurementField>();
    public DbSet<MeasurementTemplate> MeasurementTemplates => Set<MeasurementTemplate>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<MeasurementProfile> MeasurementProfiles => Set<MeasurementProfile>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemStageAssignment> OrderItemStageAssignments => Set<OrderItemStageAssignment>();
    public DbSet<OrderItemStageLog> OrderItemStageLogs => Set<OrderItemStageLog>();
    public DbSet<OrderAlteration> OrderAlterations => Set<OrderAlteration>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<StaffAttendance> StaffAttendances => Set<StaffAttendance>();
    public DbSet<ShopExpense> ShopExpenses => Set<ShopExpense>();
    public DbSet<StaffSalary> StaffSalaries => Set<StaffSalary>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Global Query Filters ────────────────────────────────────────────
        // Shop + User: TenantId only — Owner must see all branches and staff across the tenant.
        builder.Entity<Shop>().HasQueryFilter(e => e.TenantId == _tenantId && !e.IsDeleted);
        builder.Entity<Domain.Entities.User>().HasQueryFilter(e => e.TenantId == _tenantId && !e.IsDeleted);

        // Operational data: TenantId + BranchId — each branch sees only its own records.
        // BranchId == ShopId (Shop.BranchId = Shop.Id is set at provisioning time).
        builder.Entity<Customer>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<MeasurementField>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<MeasurementTemplate>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<TemplateField>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId);
        builder.Entity<MeasurementProfile>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<Order>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<OrderItem>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<OrderItemStageAssignment>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId);
        builder.Entity<OrderItemStageLog>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId);
        builder.Entity<OrderAlteration>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<OrderPayment>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId);
        builder.Entity<OrderStatusHistory>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId);
        builder.Entity<StaffAttendance>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<ShopExpense>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);
        builder.Entity<StaffSalary>().HasQueryFilter(e => e.TenantId == _tenantId && e.BranchId == _shopId && !e.IsDeleted);

        // ── Postgres array types for User.RoleIds / ShopIds ─────────────────
        builder.Entity<Domain.Entities.User>()
            .Property(e => e.RoleIds).HasColumnType("uuid[]");
        builder.Entity<Domain.Entities.User>()
            .Property(e => e.ShopIds).HasColumnType("uuid[]");

        // ── JSONB columns ────────────────────────────────────────────────────
        builder.Entity<MeasurementProfile>()
            .Property(e => e.FieldValuesJson).HasColumnType("jsonb");

        builder.Entity<OrderItem>()
            .Property(e => e.MeasurementSnapshot).HasColumnType("jsonb");

        // ── Indexes ──────────────────────────────────────────────────────────
        builder.Entity<Customer>()
            .HasIndex(e => new { e.TenantId, e.ShopId, e.Phone }).IsUnique();

        builder.Entity<MeasurementField>()
            .HasIndex(e => new { e.TenantId, e.ShopId, e.Name }).IsUnique();

        builder.Entity<TemplateField>()
            .HasIndex(e => new { e.TemplateId, e.MeasurementFieldId }).IsUnique();

        builder.Entity<Order>()
            .HasIndex(e => new { e.TenantId, e.ShopId, e.OrderNumber }).IsUnique();
        builder.Entity<Order>()
            .HasIndex(e => new { e.TenantId, e.CustomerId, e.Status });
        builder.Entity<Order>()
            .HasIndex(e => new { e.TenantId, e.ShopId, e.DeliveryDate });

        builder.Entity<OrderItem>()
            .HasIndex(e => new { e.TenantId, e.OrderId });

        // StageAssignments — one assignment per stage per item
        builder.Entity<OrderItemStageAssignment>()
            .HasIndex(e => new { e.OrderItemId, e.Stage }).IsUnique();
        builder.Entity<OrderItemStageAssignment>()
            .HasIndex(e => new { e.TenantId, e.AssignedKarigarId, e.Stage });

        // StageLogs
        builder.Entity<OrderItemStageLog>()
            .HasIndex(e => new { e.TenantId, e.OrderItemId });
        builder.Entity<OrderItemStageLog>()
            .HasIndex(e => new { e.TenantId, e.KarigarId, e.Stage });

        // OrderAlterations
        builder.Entity<OrderAlteration>()
            .HasIndex(e => new { e.TenantId, e.OrderItemId });

        // OrderPayments
        builder.Entity<OrderPayment>()
            .HasIndex(e => new { e.TenantId, e.OrderId });

        // OrderStatusHistory
        builder.Entity<OrderStatusHistory>()
            .HasIndex(e => new { e.TenantId, e.OrderId });

        // StaffAttendance — one record per staff per day
        builder.Entity<StaffAttendance>()
            .HasIndex(e => new { e.TenantId, e.StaffUserId, e.Date }).IsUnique();

        // ShopExpenses
        builder.Entity<ShopExpense>()
            .HasIndex(e => new { e.TenantId, e.ShopId, e.ExpenseDate });
        builder.Entity<ShopExpense>()
            .HasIndex(e => new { e.TenantId, e.ShopId, e.Category });

        // StaffSalaries — one per staff per month
        builder.Entity<StaffSalary>()
            .HasIndex(e => new { e.TenantId, e.StaffUserId, e.Month, e.Year }).IsUnique();
        builder.Entity<StaffSalary>()
            .HasIndex(e => new { e.TenantId, e.ShopId, e.Year, e.Month });

        // ── Relationships ────────────────────────────────────────────────────
        builder.Entity<Order>()
            .HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId);
        builder.Entity<Order>()
            .HasMany(o => o.Payments).WithOne(p => p.Order).HasForeignKey(p => p.OrderId);
        builder.Entity<Order>()
            .HasMany(o => o.Alterations).WithOne(a => a.Order).HasForeignKey(a => a.OrderId);

        builder.Entity<OrderItem>()
            .HasMany(i => i.StageAssignments).WithOne(a => a.OrderItem).HasForeignKey(a => a.OrderItemId);
        builder.Entity<OrderItem>()
            .HasMany(i => i.StageLogs).WithOne(l => l.OrderItem).HasForeignKey(l => l.OrderItemId);
        builder.Entity<OrderItem>()
            .HasMany(i => i.Alterations).WithOne(a => a.OrderItem).HasForeignKey(a => a.OrderItemId);

        builder.Entity<OrderItemStageAssignment>()
            .HasOne(a => a.AssignedKarigar).WithMany().HasForeignKey(a => a.AssignedKarigarId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<OrderItemStageLog>()
            .HasOne(l => l.Karigar).WithMany().HasForeignKey(l => l.KarigarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ShopExpense>()
            .HasOne(e => e.AddedByUser).WithMany().HasForeignKey(e => e.AddedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<StaffAttendance>()
            .HasOne(a => a.StaffUser).WithMany().HasForeignKey(a => a.StaffUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<StaffSalary>()
            .HasOne(s => s.StaffUser).WithMany().HasForeignKey(s => s.StaffUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
