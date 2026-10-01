using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Roadmap2026_09 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachment_Orders_OrderId",
                table: "Attachment");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateFields_MeasurementProfiles_MeasurementProfileId",
                table: "TemplateFields");

            migrationBuilder.DropIndex(
                name: "IX_TemplateFields_MeasurementProfileId",
                table: "TemplateFields");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Attachment",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "MeasurementProfileId",
                table: "TemplateFields");

            migrationBuilder.RenameTable(
                name: "Attachment",
                newName: "Attachments");

            migrationBuilder.RenameIndex(
                name: "IX_Attachment_OrderId",
                table: "Attachments",
                newName: "IX_Attachments_OrderId");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "StaffSalaries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "StaffSalaries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsEnabled",
                table: "Shops",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionPlanId",
                table: "Shops",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Shops",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsManualOverride",
                table: "OrderStatusHistories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledOn",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadyDate",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialDate",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrialNotes",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrialRequired",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Orders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefund",
                table: "OrderPayments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CutQuantity",
                table: "OrderItemStageLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefectNotes",
                table: "OrderItemStageLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FabricUsed",
                table: "OrderItemStageLogs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecutRequired",
                table: "OrderItemStageLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReworkReason",
                table: "OrderItemStageLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "OrderItemStageLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                table: "OrderItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FabricCharge",
                table: "OrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "FabricSource",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FittingNotes",
                table: "OrderItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeasurementProfileVersion",
                table: "OrderItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedKarigarId",
                table: "OrderAlterations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedOn",
                table: "OrderAlterations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredOn",
                table: "OrderAlterations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredChange",
                table: "OrderAlterations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "OrderAlterations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentVersion",
                table: "MeasurementProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TextValuesJson",
                table: "MeasurementProfiles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValueType",
                table: "MeasurementFields",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CustomerType",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsOptOut",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredKarigarId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredStyles",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "AppUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndsAt",
                table: "AppUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Attachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "Attachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "Attachments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "Attachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Attachments",
                table: "Attachments",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldValues = table.Column<string>(type: "jsonb", nullable: true),
                    NewValues = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    MovementAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "numeric", nullable: false),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CountedCash = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "numeric", nullable: true),
                    Variance = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GarmentAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarmentType = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OptionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarmentAttributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KarigarLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KarigarId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Stage = table.Column<int>(type: "integer", nullable: true),
                    StageLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KarigarLedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KarigarRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KarigarId = table.Column<Guid>(type: "uuid", nullable: true),
                    GarmentType = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KarigarRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeasurementProfileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    TextValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementProfileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeasurementProfileVersions_MeasurementProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "MeasurementProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Recipient = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByIp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalaryAdvances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    AdvanceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RecoveredInSalaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdvances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalaryRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "numeric", nullable: false),
                    WorkingDaysPerMonth = table.Column<int>(type: "integer", nullable: false),
                    PaidLeaveDays = table.Column<int>(type: "integer", nullable: false),
                    HalfDayFactor = table.Column<decimal>(type: "numeric", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    MaxBranches = table.Column<int>(type: "integer", nullable: true),
                    MaxUsers = table.Column<int>(type: "integer", nullable: true),
                    MaxOrdersPerMonth = table.Column<int>(type: "integer", nullable: true),
                    TrialDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: true),
                    PaymentReference = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "text", nullable: false),
                    Permission = table.Column<string>(type: "text", nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: true),
                    Design = table.Column<string>(type: "text", nullable: true),
                    Width = table.Column<decimal>(type: "numeric", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    CounterpartBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sr = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffSalaries_TenantId_BranchId_Year_Month",
                table: "StaffSalaries",
                columns: new[] { "TenantId", "BranchId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendances_TenantId_BranchId_Date",
                table: "StaffAttendances",
                columns: new[] { "TenantId", "BranchId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Shops_TenantId",
                table: "Shops",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopExpenses_TenantId_BranchId_ExpenseDate",
                table: "ShopExpenses",
                columns: new[] { "TenantId", "BranchId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber_trgm",
                table: "Orders",
                column: "OrderNumber")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId_CreatedOn",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId_DeliveryDate",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "DeliveryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId_Status",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_TenantId_BranchId_PaidAt",
                table: "OrderPayments",
                columns: new[] { "TenantId", "BranchId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemStageLogs_TenantId_BranchId_CompletedAt",
                table: "OrderItemStageLogs",
                columns: new[] { "TenantId", "BranchId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemStageAssignments_TenantId_BranchId_Stage",
                table: "OrderItemStageAssignments",
                columns: new[] { "TenantId", "BranchId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_TenantId_BranchId",
                table: "OrderItems",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderAlterations_TenantId_BranchId_Status",
                table: "OrderAlterations",
                columns: new[] { "TenantId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementTemplates_TenantId_BranchId",
                table: "MeasurementTemplates",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementProfiles_TenantId_BranchId_CustomerId",
                table: "MeasurementProfiles",
                columns: new[] { "TenantId", "BranchId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementFields_TenantId_BranchId",
                table: "MeasurementFields",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name_trgm",
                table: "Customers",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone_trgm",
                table: "Customers",
                column: "Phone")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_BranchId_CreatedOn",
                table: "Customers",
                columns: new[] { "TenantId", "BranchId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Phone",
                table: "AppUsers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_TenantId",
                table: "AppUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TenantId_BranchId_AttachmentType_EntityId",
                table: "Attachments",
                columns: new[] { "TenantId", "BranchId", "AttachmentType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_CreatedOn",
                table: "AuditLogs",
                columns: new[] { "TenantId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_TenantId_SessionId",
                table: "CashMovements",
                columns: new[] { "TenantId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_OneOpenPerBranch",
                table: "CashSessions",
                columns: new[] { "TenantId", "BranchId" },
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_TenantId_BranchId_Name",
                table: "ExpenseCategories",
                columns: new[] { "TenantId", "BranchId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_GarmentAttributes_TenantId_BranchId_GarmentType_Name",
                table: "GarmentAttributes",
                columns: new[] { "TenantId", "BranchId", "GarmentType", "Name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SupplierId",
                table: "InventoryItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId_BranchId_Category",
                table: "InventoryItems",
                columns: new[] { "TenantId", "BranchId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId_BranchId_Sku",
                table: "InventoryItems",
                columns: new[] { "TenantId", "BranchId", "Sku" },
                unique: true,
                filter: "\"Sku\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ItemId",
                table: "InventoryMovements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId_BranchId_ItemId_MovementDate",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "BranchId", "ItemId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TenantId_OrderItemId",
                table: "InventoryMovements",
                columns: new[] { "TenantId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_KarigarLedgerEntries_StageLogId",
                table: "KarigarLedgerEntries",
                column: "StageLogId",
                unique: true,
                filter: "\"StageLogId\" IS NOT NULL AND \"EntryType\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_KarigarLedgerEntries_TenantId_BranchId_KarigarId_EntryDate",
                table: "KarigarLedgerEntries",
                columns: new[] { "TenantId", "BranchId", "KarigarId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_KarigarRates_TenantId_BranchId_KarigarId_GarmentType_Stage",
                table: "KarigarRates",
                columns: new[] { "TenantId", "BranchId", "KarigarId", "GarmentType", "Stage" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementProfileVersions_ProfileId_VersionNo",
                table: "MeasurementProfileVersions",
                columns: new[] { "ProfileId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_OrderId_EventType",
                table: "NotificationMessages",
                columns: new[] { "OrderId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_Status_ScheduledAt",
                table: "NotificationMessages",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_TenantId_BranchId_CreatedOn",
                table: "NotificationMessages",
                columns: new[] { "TenantId", "BranchId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_TenantId_BranchId_EventType_Channel",
                table: "NotificationTemplates",
                columns: new[] { "TenantId", "BranchId", "EventType", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetCodes_UserId",
                table: "PasswordResetCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvances_TenantId_BranchId_StaffUserId",
                table: "SalaryAdvances",
                columns: new[] { "TenantId", "BranchId", "StaffUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRules_TenantId_BranchId_StaffUserId",
                table: "SalaryRules",
                columns: new[] { "TenantId", "BranchId", "StaffUserId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Name",
                table: "SubscriptionPlans",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_BranchId_Name",
                table: "Suppliers",
                columns: new[] { "TenantId", "BranchId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvoices_InvoiceNumber",
                table: "TenantInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvoices_TenantId_PeriodStart",
                table: "TenantInvoices",
                columns: new[] { "TenantId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantRolePermissions_TenantId_RoleName_Permission",
                table: "TenantRolePermissions",
                columns: new[] { "TenantId", "RoleName", "Permission" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Orders_OrderId",
                table: "Attachments",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Orders_OrderId",
                table: "Attachments");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "CashSessions");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "GarmentAttributes");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "KarigarLedgerEntries");

            migrationBuilder.DropTable(
                name: "KarigarRates");

            migrationBuilder.DropTable(
                name: "MeasurementProfileVersions");

            migrationBuilder.DropTable(
                name: "NotificationMessages");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "PasswordResetCodes");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SalaryAdvances");

            migrationBuilder.DropTable(
                name: "SalaryRules");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "TenantInvoices");

            migrationBuilder.DropTable(
                name: "TenantRolePermissions");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_StaffSalaries_TenantId_BranchId_Year_Month",
                table: "StaffSalaries");

            migrationBuilder.DropIndex(
                name: "IX_StaffAttendances_TenantId_BranchId_Date",
                table: "StaffAttendances");

            migrationBuilder.DropIndex(
                name: "IX_Shops_TenantId",
                table: "Shops");

            migrationBuilder.DropIndex(
                name: "IX_ShopExpenses_TenantId_BranchId_ExpenseDate",
                table: "ShopExpenses");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderNumber_trgm",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_CreatedOn",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_DeliveryDate",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderPayments_TenantId_BranchId_PaidAt",
                table: "OrderPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrderItemStageLogs_TenantId_BranchId_CompletedAt",
                table: "OrderItemStageLogs");

            migrationBuilder.DropIndex(
                name: "IX_OrderItemStageAssignments_TenantId_BranchId_Stage",
                table: "OrderItemStageAssignments");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_TenantId_BranchId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderAlterations_TenantId_BranchId_Status",
                table: "OrderAlterations");

            migrationBuilder.DropIndex(
                name: "IX_MeasurementTemplates_TenantId_BranchId",
                table: "MeasurementTemplates");

            migrationBuilder.DropIndex(
                name: "IX_MeasurementProfiles_TenantId_BranchId_CustomerId",
                table: "MeasurementProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MeasurementFields_TenantId_BranchId",
                table: "MeasurementFields");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Name_trgm",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Phone_trgm",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_BranchId_CreatedOn",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_Phone",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_TenantId",
                table: "AppUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Attachments",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_TenantId_BranchId_AttachmentType_EntityId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "StaffSalaries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "StaffSalaries");

            migrationBuilder.DropColumn(
                name: "NotificationsEnabled",
                table: "Shops");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "Shops");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Shops");

            migrationBuilder.DropColumn(
                name: "IsManualOverride",
                table: "OrderStatusHistories");

            migrationBuilder.DropColumn(
                name: "CancelledOn",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReadyDate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrialDate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrialNotes",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrialRequired",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsRefund",
                table: "OrderPayments");

            migrationBuilder.DropColumn(
                name: "CutQuantity",
                table: "OrderItemStageLogs");

            migrationBuilder.DropColumn(
                name: "DefectNotes",
                table: "OrderItemStageLogs");

            migrationBuilder.DropColumn(
                name: "FabricUsed",
                table: "OrderItemStageLogs");

            migrationBuilder.DropColumn(
                name: "RecutRequired",
                table: "OrderItemStageLogs");

            migrationBuilder.DropColumn(
                name: "ReworkReason",
                table: "OrderItemStageLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrderItemStageLogs");

            migrationBuilder.DropColumn(
                name: "AttributesJson",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "FabricCharge",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "FabricSource",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "FittingNotes",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "MeasurementProfileVersion",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "AssignedKarigarId",
                table: "OrderAlterations");

            migrationBuilder.DropColumn(
                name: "CompletedOn",
                table: "OrderAlterations");

            migrationBuilder.DropColumn(
                name: "DeliveredOn",
                table: "OrderAlterations");

            migrationBuilder.DropColumn(
                name: "RequiredChange",
                table: "OrderAlterations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrderAlterations");

            migrationBuilder.DropColumn(
                name: "CurrentVersion",
                table: "MeasurementProfiles");

            migrationBuilder.DropColumn(
                name: "TextValuesJson",
                table: "MeasurementProfiles");

            migrationBuilder.DropColumn(
                name: "ValueType",
                table: "MeasurementFields");

            migrationBuilder.DropColumn(
                name: "CustomerType",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "NotificationsOptOut",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredKarigarId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredStyles",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "LockoutEndsAt",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "Attachments");

            migrationBuilder.RenameTable(
                name: "Attachments",
                newName: "Attachment");

            migrationBuilder.RenameIndex(
                name: "IX_Attachments_OrderId",
                table: "Attachment",
                newName: "IX_Attachment_OrderId");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "MeasurementProfileId",
                table: "TemplateFields",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Attachment",
                table: "Attachment",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFields_MeasurementProfileId",
                table: "TemplateFields",
                column: "MeasurementProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachment_Orders_OrderId",
                table: "Attachment",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateFields_MeasurementProfiles_MeasurementProfileId",
                table: "TemplateFields",
                column: "MeasurementProfileId",
                principalTable: "MeasurementProfiles",
                principalColumn: "Id");
        }
    }
}
