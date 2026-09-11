using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotPayslipCompanyIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyAddress",
                table: "mp_TblPayrollRun",
                type: "nvarchar(800)",
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EsiCode",
                table: "mp_TblPayrollRun",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PfEstablishmentCode",
                table: "mp_TblPayrollRun",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyAddress",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "EsiCode",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "PfEstablishmentCode",
                table: "mp_TblPayrollRun");
        }
    }
}
