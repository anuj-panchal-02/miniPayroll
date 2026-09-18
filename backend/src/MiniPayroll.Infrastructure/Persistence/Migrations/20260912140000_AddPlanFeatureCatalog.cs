using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Subscriptions;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniPayrollDbContext))]
    [Migration("20260912140000_AddPlanFeatureCatalog")]
    public class AddPlanFeatureCatalog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Limit",
                table: TableNames.PlanFeature,
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE mp_TblPlanFeature
                SET Code = N'PAYROLL', IsEnabled = 1
                WHERE LOWER(Code) = N'payroll';
                """);

            foreach (var code in PlanFeatureCodes.CoreEnabled)
            {
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO mp_TblPlanFeature (Id, PlanId, Code, IsEnabled, [Limit])
                    SELECT NEWID(), p.Id, N'{code}', 1, NULL
                    FROM mp_TblPlan p
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM mp_TblPlanFeature feature
                        WHERE feature.PlanId = p.Id
                          AND UPPER(feature.Code) = N'{code}');
                    """);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var code in PlanFeatureCodes.CoreEnabled.Where(item => item != PlanFeatureCodes.Payroll))
            {
                migrationBuilder.Sql(
                    $"""
                    DELETE FROM mp_TblPlanFeature
                    WHERE UPPER(Code) = N'{code}';
                    """);
            }

            migrationBuilder.Sql(
                """
                UPDATE mp_TblPlanFeature
                SET Code = N'payroll'
                WHERE Code = N'PAYROLL';
                """);

            migrationBuilder.DropColumn(name: "Limit", table: TableNames.PlanFeature);
        }
    }
}
