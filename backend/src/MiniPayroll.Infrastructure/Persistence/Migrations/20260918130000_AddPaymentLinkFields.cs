using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPayroll.Domain.Constants;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniPayrollDbContext))]
    [Migration("20260918130000_AddPaymentLinkFields")]
    public class AddPaymentLinkFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentLinkId",
                table: TableNames.PaymentIntent,
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: TableNames.PaymentIntent,
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentIntent_ProviderPaymentLinkId",
                table: TableNames.PaymentIntent,
                column: "ProviderPaymentLinkId",
                unique: true,
                filter: "[ProviderPaymentLinkId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mp_TblPaymentIntent_ProviderPaymentLinkId",
                table: TableNames.PaymentIntent);

            migrationBuilder.DropColumn(
                name: "ProviderPaymentLinkId",
                table: TableNames.PaymentIntent);

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: TableNames.PaymentIntent);
        }
    }
}
