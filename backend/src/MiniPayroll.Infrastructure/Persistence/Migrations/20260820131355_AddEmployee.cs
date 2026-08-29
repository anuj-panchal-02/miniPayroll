using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mp_TblEmployee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmploymentType = table.Column<int>(type: "int", nullable: false),
                    JoiningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Ifsc = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UpiId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OvertimeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblEmployee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblEmployee_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "mp_TblCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblEmployee_CompanyId_EmployeeCode",
                table: "mp_TblEmployee",
                columns: new[] { "CompanyId", "EmployeeCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mp_TblEmployee");
        }
    }
}
