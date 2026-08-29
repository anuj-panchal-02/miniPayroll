using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySetupProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SetupStep",
                table: "mp_TblCompany",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Companies finished before SetupStep existed are already at CompanySetupStep.Complete.
            migrationBuilder.Sql(
                "UPDATE [mp_TblCompany] SET [SetupStep] = 3 WHERE [IsSetupComplete] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SetupStep",
                table: "mp_TblCompany");
        }
    }
}
