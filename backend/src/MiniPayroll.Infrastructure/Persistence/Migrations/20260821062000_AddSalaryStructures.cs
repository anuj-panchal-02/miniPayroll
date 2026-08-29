using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniPayrollDbContext))]
    [Migration("20260821062000_AddSalaryStructures")]
    public partial class AddSalaryStructures : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mp_TblSalaryComponent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IsStandardPreset = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblSalaryComponent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblSalaryComponent_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "mp_TblCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblSalaryStructure",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblSalaryStructure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblSalaryStructure_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "mp_TblCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblSalaryStructure_mp_TblEmployee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "mp_TblEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblEmployeeSalaryComponent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalaryStructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalaryComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ValueType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblEmployeeSalaryComponent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblEmployeeSalaryComponent_mp_TblSalaryComponent_SalaryComponentId",
                        column: x => x.SalaryComponentId,
                        principalTable: "mp_TblSalaryComponent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblEmployeeSalaryComponent_mp_TblSalaryStructure_SalaryStructureId",
                        column: x => x.SalaryStructureId,
                        principalTable: "mp_TblSalaryStructure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSalaryComponent_CompanyId_Name_Type",
                table: "mp_TblSalaryComponent",
                columns: new[] { "CompanyId", "Name", "Type" }, unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSalaryStructure_CompanyId_EmployeeId",
                table: "mp_TblSalaryStructure",
                columns: new[] { "CompanyId", "EmployeeId" });
            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSalaryStructure_EmployeeId_EffectiveFrom",
                table: "mp_TblSalaryStructure",
                columns: new[] { "EmployeeId", "EffectiveFrom" }, unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_mp_TblEmployeeSalaryComponent_SalaryComponentId",
                table: "mp_TblEmployeeSalaryComponent", column: "SalaryComponentId");
            migrationBuilder.CreateIndex(
                name: "IX_mp_TblEmployeeSalaryComponent_SalaryStructureId_SortOrder",
                table: "mp_TblEmployeeSalaryComponent",
                columns: new[] { "SalaryStructureId", "SortOrder" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "mp_TblEmployeeSalaryComponent");
            migrationBuilder.DropTable(name: "mp_TblSalaryComponent");
            migrationBuilder.DropTable(name: "mp_TblSalaryStructure");
        }
    }
}
