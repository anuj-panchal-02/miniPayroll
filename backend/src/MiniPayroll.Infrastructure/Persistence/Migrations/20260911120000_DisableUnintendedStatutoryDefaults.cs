using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// AddStatutoryPayroll filled existing rows with PF/ESI = true. Automated statutory
    /// did not exist before that migration, so those values were not an admin choice.
    /// Backfill coverage off and make new SQL inserts default to false. Snapshot payroll
    /// rows are not rewritten; finalized payslips stay as stored.
    /// </summary>
    public partial class DisableUnintendedStatutoryDefaults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [mp_TblCompany] SET [PfApplicable] = 0, [EsiApplicable] = 0;");
            migrationBuilder.Sql("UPDATE [mp_TblEmployee] SET [PfCovered] = 0, [EsiCovered] = 0;");

            migrationBuilder.AlterColumn<bool>(
                name: "PfApplicable",
                table: "mp_TblCompany",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "EsiApplicable",
                table: "mp_TblCompany",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "PfCovered",
                table: "mp_TblEmployee",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "EsiCovered",
                table: "mp_TblEmployee",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "PfApplicable",
                table: "mp_TblCompany",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "EsiApplicable",
                table: "mp_TblCompany",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "PfCovered",
                table: "mp_TblEmployee",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "EsiCovered",
                table: "mp_TblEmployee",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);
        }
    }
}
