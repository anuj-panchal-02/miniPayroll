using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutoryPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EmployerEsi",
                table: "mp_TblPayrollEmployee",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerPf",
                table: "mp_TblPayrollEmployee",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ComputedAmount",
                table: "mp_TblPayrollDeduction",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatutoryKind",
                table: "mp_TblPayrollDeduction",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "mp_TblEmployeeSalaryComponent",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EsiCovered",
                table: "mp_TblEmployee",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "EsiNumber",
                table: "mp_TblEmployee",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "mp_TblEmployee",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PfCovered",
                table: "mp_TblEmployee",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PfNumber",
                table: "mp_TblEmployee",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uan",
                table: "mp_TblEmployee",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsiApplicable",
                table: "mp_TblCompany",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "EsiCode",
                table: "mp_TblCompany",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PfApplicable",
                table: "mp_TblCompany",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PfEstablishmentCode",
                table: "mp_TblCompany",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PfUseWageCeiling",
                table: "mp_TblCompany",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "mp_TblPayrollStatutoryOverride",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPayrollStatutoryOverride", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPayrollStatutoryOverride_mp_TblEmployee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "mp_TblEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblPayrollStatutoryOverride_mp_TblPayrollRun_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "mp_TblPayrollRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayrollStatutoryOverride_EmployeeId",
                table: "mp_TblPayrollStatutoryOverride",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayrollStatutoryOverride_PayrollRunId_EmployeeId_Kind",
                table: "mp_TblPayrollStatutoryOverride",
                columns: new[] { "PayrollRunId", "EmployeeId", "Kind" },
                unique: true);

            migrationBuilder.Sql("""
                UPDATE mp_TblEmployeeSalaryComponent SET Kind = 1 WHERE Name IN (N'Basic Salary', N'Basic');
                UPDATE mp_TblEmployeeSalaryComponent SET Kind = 2 WHERE Name IN (N'Dearness Allowance (DA)', N'Dearness Allowance', N'DA');
                UPDATE mp_TblEmployeeSalaryComponent SET Kind = 3 WHERE Name IN (N'HRA', N'House Rent Allowance');
                UPDATE mp_TblEmployeeSalaryComponent SET Kind = 4 WHERE Name IN (N'Conveyance Allowance', N'Conveyance');
                UPDATE mp_TblEmployeeSalaryComponent SET Kind = 5 WHERE Name = N'Special Allowance';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mp_TblPayrollStatutoryOverride");

            migrationBuilder.DropColumn(
                name: "EmployerEsi",
                table: "mp_TblPayrollEmployee");

            migrationBuilder.DropColumn(
                name: "EmployerPf",
                table: "mp_TblPayrollEmployee");

            migrationBuilder.DropColumn(
                name: "ComputedAmount",
                table: "mp_TblPayrollDeduction");

            migrationBuilder.DropColumn(
                name: "StatutoryKind",
                table: "mp_TblPayrollDeduction");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "mp_TblEmployeeSalaryComponent");

            migrationBuilder.DropColumn(
                name: "EsiCovered",
                table: "mp_TblEmployee");

            migrationBuilder.DropColumn(
                name: "EsiNumber",
                table: "mp_TblEmployee");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "mp_TblEmployee");

            migrationBuilder.DropColumn(
                name: "PfCovered",
                table: "mp_TblEmployee");

            migrationBuilder.DropColumn(
                name: "PfNumber",
                table: "mp_TblEmployee");

            migrationBuilder.DropColumn(
                name: "Uan",
                table: "mp_TblEmployee");

            migrationBuilder.DropColumn(
                name: "EsiApplicable",
                table: "mp_TblCompany");

            migrationBuilder.DropColumn(
                name: "EsiCode",
                table: "mp_TblCompany");

            migrationBuilder.DropColumn(
                name: "PfApplicable",
                table: "mp_TblCompany");

            migrationBuilder.DropColumn(
                name: "PfEstablishmentCode",
                table: "mp_TblCompany");

            migrationBuilder.DropColumn(
                name: "PfUseWageCeiling",
                table: "mp_TblCompany");
        }
    }
}
