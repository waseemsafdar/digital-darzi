using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignupBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSelfService",
                table: "TenantInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProofContentType",
                table: "TenantInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofFileName",
                table: "TenantInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofNote",
                table: "TenantInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofPaidVia",
                table: "TenantInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofReference",
                table: "TenantInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofStorageKey",
                table: "TenantInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProofSubmittedAt",
                table: "TenantInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "TenantInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "TenantInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesPhotos",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesReports",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreeTier",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Free plan: where a business lands when its trial or paid period ends (2026-11).
            migrationBuilder.Sql(@"
INSERT INTO ""SubscriptionPlans"" (""Id"", ""Name"", ""Description"", ""MonthlyPrice"", ""Currency"", ""MaxBranches"", ""MaxUsers"",
    ""MaxOrdersPerMonth"", ""TrialDays"", ""IsActive"", ""CreatedOn"", ""IsFreeTier"", ""IncludesPhotos"", ""IncludesReports"")
SELECT gen_random_uuid(), 'Free', 'Free forever: 1 branch, owner plus 2 staff, 50 orders a month. No photos or reports.',
    0, 'PKR', 1, 3, 50, 0, TRUE, now() AT TIME ZONE 'utc', TRUE, FALSE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM ""SubscriptionPlans"" WHERE ""IsFreeTier"" OR ""Name"" = 'Free');
UPDATE ""SubscriptionPlans"" SET ""IsFreeTier"" = TRUE
WHERE ""Name"" = 'Free' AND NOT EXISTS (SELECT 1 FROM ""SubscriptionPlans"" WHERE ""IsFreeTier"");");

            // Built-in roles added 2026-11. The hosted server never runs startup seeding, so add them here.
            migrationBuilder.Sql(@"
INSERT INTO ""AspNetRoles"" (""Id"", ""Name"", ""NormalizedName"", ""ConcurrencyStamp"")
SELECT gen_random_uuid(), r.name, upper(r.name), gen_random_uuid()::text
FROM (VALUES ('Master'), ('Cutter'), ('KajButton'), ('Embroiderer'), ('Presser'), ('Helper'), ('Salesman'), ('Munshi'), ('Delivery')) AS r(name)
WHERE NOT EXISTS (SELECT 1 FROM ""AspNetRoles"" a WHERE a.""NormalizedName"" = upper(r.name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSelfService",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "ProofContentType",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "ProofFileName",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "ProofNote",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "ProofPaidVia",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "ProofReference",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "ProofStorageKey",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "ProofSubmittedAt",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "TenantInvoices");

            migrationBuilder.DropColumn(
                name: "IncludesPhotos",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "IncludesReports",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "IsFreeTier",
                table: "SubscriptionPlans");
        }
    }
}
