using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPayroll.Domain.Constants;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniPayrollDbContext))]
    [Migration("20260912180000_AddInvoiceLedger")]
    public class AddInvoiceLedger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: TableNames.Invoice,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Tax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExternalInvoiceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblInvoice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblInvoice_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: TableNames.Company,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblInvoice_mp_TblSubscription_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: TableNames.Subscription,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: TableNames.InvoiceLine,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblInvoiceLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblInvoiceLine_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: TableNames.Company,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblInvoiceLine_mp_TblInvoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: TableNames.Invoice,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblInvoice_InvoiceNumber",
                table: TableNames.Invoice,
                column: "InvoiceNumber",
                unique: true,
                filter: "[InvoiceNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblInvoice_CompanyId_PeriodStart_PeriodEnd",
                table: TableNames.Invoice,
                columns: new[] { "CompanyId", "PeriodStart", "PeriodEnd" },
                unique: true,
                filter: "[Status] <> 6");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblInvoice_SubscriptionId",
                table: TableNames.Invoice,
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblInvoiceLine_CompanyId",
                table: TableNames.InvoiceLine,
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblInvoiceLine_InvoiceId",
                table: TableNames.InvoiceLine,
                column: "InvoiceId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: TableNames.InvoiceLine);
            migrationBuilder.DropTable(name: TableNames.Invoice);
        }
    }
}
