using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPayroll.Domain.Constants;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniPayrollDbContext))]
    [Migration("20260912200000_AddPaymentIntentAndProviderEvents")]
    public class AddPaymentIntentAndProviderEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: TableNames.PaymentIntent,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderOrderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProviderPaymentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProviderSubscriptionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPaymentIntent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPaymentIntent_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: TableNames.Company,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblPaymentIntent_mp_TblSubscription_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: TableNames.Subscription,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblPaymentIntent_mp_TblInvoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: TableNames.Invoice,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: TableNames.PaymentProviderEvent,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderEventId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPaymentProviderEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPaymentProviderEvent_mp_TblPaymentIntent_IntentId",
                        column: x => x.IntentId,
                        principalTable: TableNames.PaymentIntent,
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentIntent_IdempotencyKey",
                table: TableNames.PaymentIntent,
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentIntent_ProviderOrderId",
                table: TableNames.PaymentIntent,
                column: "ProviderOrderId",
                unique: true,
                filter: "[ProviderOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentIntent_CompanyId",
                table: TableNames.PaymentIntent,
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentIntent_SubscriptionId",
                table: TableNames.PaymentIntent,
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentIntent_InvoiceId",
                table: TableNames.PaymentIntent,
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentProviderEvent_ProviderEventId",
                table: TableNames.PaymentProviderEvent,
                column: "ProviderEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentProviderEvent_IntentId",
                table: TableNames.PaymentProviderEvent,
                column: "IntentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: TableNames.PaymentProviderEvent);
            migrationBuilder.DropTable(name: TableNames.PaymentIntent);
        }
    }
}
