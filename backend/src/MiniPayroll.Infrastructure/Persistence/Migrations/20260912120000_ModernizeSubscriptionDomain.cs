using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Subscriptions;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniPayrollDbContext))]
    [Migration("20260912120000_ModernizeSubscriptionDomain")]
    public class ModernizeSubscriptionDomain : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: TableNames.Plan,
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: TableNames.Plan,
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: TableNames.Plan,
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxActiveEmployees",
                table: TableNames.Plan,
                type: "int",
                nullable: false,
                defaultValue: PlatformLimits.DefaultEmployeeLimit);

            migrationBuilder.AddColumn<int>(
                name: "TrialDays",
                table: TableNames.Plan,
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE p
                SET
                    Code = CASE
                        WHEN p.Name = N'Basic' THEN N'basic'
                        ELSE LOWER(REPLACE(REPLACE(LTRIM(RTRIM(p.Name)), N' ', N'-'), N'_', N'-'))
                    END,
                    IsActive = p.IsPublic,
                    MaxActiveEmployees = CASE
                        WHEN p.DefaultEmployeeLimit >= 1 THEN p.DefaultEmployeeLimit
                        ELSE 50
                    END,
                    TrialDays = 0
                FROM mp_TblPlan p;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: TableNames.Plan,
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPlan_Code",
                table: TableNames.Plan,
                column: "Code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_mp_TblPlan_TrialDays",
                table: TableNames.Plan,
                sql: "[TrialDays] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_mp_TblPlan_MaxActiveEmployees",
                table: TableNames.Plan,
                sql: "[MaxActiveEmployees] >= 1");

            migrationBuilder.AddColumn<int>(
                name: "BillingCycle",
                table: TableNames.Subscription,
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextBillingDate",
                table: TableNames.Subscription,
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialStartedAt",
                table: TableNames.Subscription,
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialEndsAt",
                table: TableNames.Subscription,
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: TableNames.Subscription,
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: TableNames.Subscription,
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: TableNames.Subscription,
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: TableNames.Subscription,
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero));

            migrationBuilder.Sql(
                """
                UPDATE s
                SET
                    BillingCycle = 0,
                    CancelAtPeriodEnd = 0,
                    NextBillingDate = COALESCE(s.DueDate, s.CurrentPeriodEnd),
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = SYSUTCDATETIME()
                FROM mp_TblSubscription s
                INNER JOIN mp_TblCompany c ON c.Id = s.CompanyId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSubscription_Status",
                table: TableNames.Subscription,
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSubscription_NextBillingDate",
                table: TableNames.Subscription,
                column: "NextBillingDate");

            migrationBuilder.AddCheckConstraint(
                name: "CK_mp_TblSubscription_BillingCycle",
                table: TableNames.Subscription,
                sql: "[BillingCycle] IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_mp_TblSubscription_Status",
                table: TableNames.Subscription,
                sql: "[Status] IN (0, 1, 2, 3, 4, 5, 6)");

            migrationBuilder.CreateTable(
                name: TableNames.PlanPrice,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPlanPrice", x => x.Id);
                    table.CheckConstraint("CK_mp_TblPlanPrice_Amount", "[Amount] >= 0");
                    table.CheckConstraint(
                        "CK_mp_TblPlanPrice_Window",
                        "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_mp_TblPlanPrice_mp_TblPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: TableNames.Plan,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPlanPrice_PlanId_BillingCycle_EffectiveFrom",
                table: TableNames.PlanPrice,
                columns: ["PlanId", "BillingCycle", "EffectiveFrom"]);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPlanPrice_PlanId_BillingCycle",
                table: TableNames.PlanPrice,
                columns: ["PlanId", "BillingCycle"],
                unique: true,
                filter: "[IsActive] = 1 AND [EffectiveTo] IS NULL");

            migrationBuilder.CreateTable(
                name: TableNames.PlanFeature,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPlanFeature", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPlanFeature_mp_TblPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: TableNames.Plan,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPlanFeature_PlanId_Code",
                table: TableNames.PlanFeature,
                columns: ["PlanId", "Code"],
                unique: true);

            migrationBuilder.CreateTable(
                name: TableNames.SubscriptionEvent,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblSubscriptionEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblSubscriptionEvent_mp_TblSubscription_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: TableNames.Subscription,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblSubscriptionEvent_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: TableNames.Company,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSubscriptionEvent_SubscriptionId_OccurredAt",
                table: TableNames.SubscriptionEvent,
                columns: ["SubscriptionId", "OccurredAt"]);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSubscriptionEvent_CompanyId_OccurredAt",
                table: TableNames.SubscriptionEvent,
                columns: ["CompanyId", "OccurredAt"]);

            migrationBuilder.Sql(
                $"""
                INSERT INTO mp_TblPlanPrice
                    (Id, PlanId, BillingCycle, Amount, Currency, EffectiveFrom, EffectiveTo, IsActive)
                SELECT
                    NEWID(),
                    p.Id,
                    0,
                    p.PricePerEmployee,
                    N'{PlatformLimits.CurrencyCode}',
                    '{SubscriptionSchemaBackfill.OpenPriceWindow:yyyy-MM-dd HH:mm:ss.fffffff zzz}',
                    NULL,
                    1
                FROM mp_TblPlan p
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM mp_TblPlanPrice price
                    WHERE price.PlanId = p.Id AND price.BillingCycle = 0);

                INSERT INTO mp_TblPlanFeature (Id, PlanId, Code, IsEnabled)
                SELECT NEWID(), p.Id, N'{PlanFeatureCodes.Payroll}', 1
                FROM mp_TblPlan p
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM mp_TblPlanFeature feature
                    WHERE feature.PlanId = p.Id AND feature.Code = N'{PlanFeatureCodes.Payroll}');

                INSERT INTO mp_TblSubscriptionEvent
                    (Id, SubscriptionId, CompanyId, Type, OccurredAt, ActorUserId)
                SELECT
                    NEWID(),
                    s.Id,
                    s.CompanyId,
                    1,
                    COALESCE(c.ActivatedAt, s.CurrentPeriodStart, s.CreatedAt),
                    NULL
                FROM mp_TblSubscription s
                INNER JOIN mp_TblCompany c ON c.Id = s.CompanyId
                WHERE s.Status = 1;

                INSERT INTO mp_TblSubscriptionEvent
                    (Id, SubscriptionId, CompanyId, Type, OccurredAt, ActorUserId)
                SELECT
                    NEWID(),
                    s.Id,
                    s.CompanyId,
                    6,
                    COALESCE(s.CancelledAt, s.UpdatedAt),
                    NULL
                FROM mp_TblSubscription s
                WHERE s.Status = 4;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: TableNames.SubscriptionEvent);
            migrationBuilder.DropTable(name: TableNames.PlanFeature);
            migrationBuilder.DropTable(name: TableNames.PlanPrice);

            migrationBuilder.DropCheckConstraint("CK_mp_TblSubscription_BillingCycle", TableNames.Subscription);
            migrationBuilder.DropCheckConstraint("CK_mp_TblSubscription_Status", TableNames.Subscription);
            migrationBuilder.DropIndex("IX_mp_TblSubscription_Status", TableNames.Subscription);
            migrationBuilder.DropIndex("IX_mp_TblSubscription_NextBillingDate", TableNames.Subscription);
            migrationBuilder.DropColumn("BillingCycle", TableNames.Subscription);
            migrationBuilder.DropColumn("NextBillingDate", TableNames.Subscription);
            migrationBuilder.DropColumn("TrialStartedAt", TableNames.Subscription);
            migrationBuilder.DropColumn("TrialEndsAt", TableNames.Subscription);
            migrationBuilder.DropColumn("CancelledAt", TableNames.Subscription);
            migrationBuilder.DropColumn("CancelAtPeriodEnd", TableNames.Subscription);
            migrationBuilder.DropColumn("CreatedAt", TableNames.Subscription);
            migrationBuilder.DropColumn("UpdatedAt", TableNames.Subscription);

            migrationBuilder.DropCheckConstraint("CK_mp_TblPlan_TrialDays", TableNames.Plan);
            migrationBuilder.DropCheckConstraint("CK_mp_TblPlan_MaxActiveEmployees", TableNames.Plan);
            migrationBuilder.DropIndex("IX_mp_TblPlan_Code", TableNames.Plan);
            migrationBuilder.DropColumn("Code", TableNames.Plan);
            migrationBuilder.DropColumn("Description", TableNames.Plan);
            migrationBuilder.DropColumn("IsActive", TableNames.Plan);
            migrationBuilder.DropColumn("MaxActiveEmployees", TableNames.Plan);
            migrationBuilder.DropColumn("TrialDays", TableNames.Plan);
        }
    }
}
