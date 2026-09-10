using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mp_TblPayrollRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DailyRateMethod = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPayrollRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPayrollRun_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "mp_TblCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblBonus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblBonus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblBonus_mp_TblEmployee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "mp_TblEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblBonus_mp_TblPayrollRun_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "mp_TblPayrollRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblDeduction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblDeduction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblDeduction_mp_TblEmployee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "mp_TblEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblDeduction_mp_TblPayrollRun_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "mp_TblPayrollRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblMonthlyAttendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkingDays = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    Present = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    PaidLeave = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    UnpaidLeave = table.Column<decimal>(type: "decimal(5,1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblMonthlyAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblMonthlyAttendance_mp_TblEmployee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "mp_TblEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblMonthlyAttendance_mp_TblPayrollRun_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "mp_TblPayrollRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblOvertime",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblOvertime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblOvertime_mp_TblEmployee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "mp_TblEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblOvertime_mp_TblPayrollRun_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "mp_TblPayrollRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblPayrollEmployee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DaysEmployed = table.Column<int>(type: "int", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    GrossEarnings = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Warnings = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Errors = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPayrollEmployee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPayrollEmployee_mp_TblEmployee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "mp_TblEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblPayrollEmployee_mp_TblPayrollRun_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "mp_TblPayrollRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblPayrollDeduction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPayrollDeduction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPayrollDeduction_mp_TblPayrollEmployee_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "mp_TblPayrollEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblPayrollEarning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPayrollEarning", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPayrollEarning_mp_TblPayrollEmployee_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "mp_TblPayrollEmployee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblBonus_EmployeeId",
                table: "mp_TblBonus",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblBonus_PayrollRunId_EmployeeId",
                table: "mp_TblBonus",
                columns: new[] { "PayrollRunId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblDeduction_EmployeeId",
                table: "mp_TblDeduction",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblDeduction_PayrollRunId_EmployeeId",
                table: "mp_TblDeduction",
                columns: new[] { "PayrollRunId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblMonthlyAttendance_EmployeeId",
                table: "mp_TblMonthlyAttendance",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblMonthlyAttendance_PayrollRunId_EmployeeId",
                table: "mp_TblMonthlyAttendance",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblOvertime_EmployeeId",
                table: "mp_TblOvertime",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblOvertime_PayrollRunId_EmployeeId",
                table: "mp_TblOvertime",
                columns: new[] { "PayrollRunId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayrollDeduction_PayrollEmployeeId_SortOrder",
                table: "mp_TblPayrollDeduction",
                columns: new[] { "PayrollEmployeeId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayrollEarning_PayrollEmployeeId_SortOrder",
                table: "mp_TblPayrollEarning",
                columns: new[] { "PayrollEmployeeId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayrollEmployee_EmployeeId",
                table: "mp_TblPayrollEmployee",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayrollEmployee_PayrollRunId_EmployeeId",
                table: "mp_TblPayrollEmployee",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayrollRun_CompanyId_Year_Month",
                table: "mp_TblPayrollRun",
                columns: new[] { "CompanyId", "Year", "Month" },
                unique: true,
                filter: "[Status] <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mp_TblBonus");

            migrationBuilder.DropTable(
                name: "mp_TblDeduction");

            migrationBuilder.DropTable(
                name: "mp_TblMonthlyAttendance");

            migrationBuilder.DropTable(
                name: "mp_TblOvertime");

            migrationBuilder.DropTable(
                name: "mp_TblPayrollDeduction");

            migrationBuilder.DropTable(
                name: "mp_TblPayrollEarning");

            migrationBuilder.DropTable(
                name: "mp_TblPayrollEmployee");

            migrationBuilder.DropTable(
                name: "mp_TblPayrollRun");
        }
    }
}
