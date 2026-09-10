using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyLogoPath",
                table: "mp_TblPayrollRun",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "mp_TblPayrollRun",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "mp_TblPayrollRun",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalizedByUserId",
                table: "mp_TblPayrollRun",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "mp_TblPayrollRun",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "mp_TblPayrollRun",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByUserId",
                table: "mp_TblPayrollRun",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Designation",
                table: "mp_TblPayrollEmployee",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "PaidOn",
                table: "mp_TblPayrollEmployee",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMode",
                table: "mp_TblPayrollEmployee",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "mp_TblPayrollEmployee",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "mp_TblPayrollEmployee",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyLogoPath",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "FinalizedByUserId",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "mp_TblPayrollRun");

            migrationBuilder.DropColumn(
                name: "Designation",
                table: "mp_TblPayrollEmployee");

            migrationBuilder.DropColumn(
                name: "PaidOn",
                table: "mp_TblPayrollEmployee");

            migrationBuilder.DropColumn(
                name: "PaymentMode",
                table: "mp_TblPayrollEmployee");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "mp_TblPayrollEmployee");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "mp_TblPayrollEmployee");
        }
    }
}
