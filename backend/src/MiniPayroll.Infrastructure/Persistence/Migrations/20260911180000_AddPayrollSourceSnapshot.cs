using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniPayrollDbContext))]
    [Migration("20260911180000_AddPayrollSourceSnapshot")]
    public class AddPayrollSourceSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EsiRuleEffectiveFrom",
                table: "mp_TblPayrollRun",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PfRuleEffectiveFrom",
                table: "mp_TblPayrollRun",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                table: "mp_TblPayrollRun",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSnapshotJson",
                table: "mp_TblPayrollRun",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsiRuleEffectiveFrom",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "PfRuleEffectiveFrom",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "SourceSnapshotJson",
                table: "mp_TblPayrollRun");
        }
    }
}
