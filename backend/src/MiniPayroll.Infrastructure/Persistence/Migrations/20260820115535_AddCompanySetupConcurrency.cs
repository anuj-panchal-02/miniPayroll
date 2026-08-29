using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySetupConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "mp_TblCompany",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblAuditLog_CompanyId_Action",
                table: "mp_TblAuditLog",
                columns: new[] { "CompanyId", "Action" },
                unique: true,
                filter: "[Action] = 'company.setup.complete'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mp_TblAuditLog_CompanyId_Action",
                table: "mp_TblAuditLog");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "mp_TblCompany");
        }
    }
}
