using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniPayroll.Domain.Constants;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MiniPayrollDbContext))]
    [Migration("20260912210000_ExpandPaymentProviderEvents")]
    public class ExpandPaymentProviderEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mp_TblPaymentProviderEvent_ProviderEventId",
                table: TableNames.PaymentProviderEvent);

            migrationBuilder.RenameColumn(
                name: "ProviderEventId",
                table: TableNames.PaymentProviderEvent,
                newName: "ExternalEventId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentProviderEvent_ExternalEventId",
                table: TableNames.PaymentProviderEvent,
                column: "ExternalEventId",
                unique: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: TableNames.PaymentProviderEvent,
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ProcessedAt",
                table: TableNames.PaymentProviderEvent,
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: TableNames.PaymentProviderEvent,
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "razorpay");

            migrationBuilder.AddColumn<int>(
                name: "EventType",
                table: TableNames.PaymentProviderEvent,
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStatus",
                table: TableNames.PaymentProviderEvent,
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: TableNames.PaymentProviderEvent,
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: TableNames.PaymentProviderEvent,
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceivedAt",
                table: TableNames.PaymentProviderEvent,
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero));

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                table: TableNames.PaymentProviderEvent,
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: TableNames.PaymentProviderEvent,
                type: "uniqueidentifier",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Provider", table: TableNames.PaymentProviderEvent);
            migrationBuilder.DropColumn(name: "EventType", table: TableNames.PaymentProviderEvent);
            migrationBuilder.DropColumn(name: "ProcessingStatus", table: TableNames.PaymentProviderEvent);
            migrationBuilder.DropColumn(name: "PayloadHash", table: TableNames.PaymentProviderEvent);
            migrationBuilder.DropColumn(name: "Error", table: TableNames.PaymentProviderEvent);
            migrationBuilder.DropColumn(name: "ReceivedAt", table: TableNames.PaymentProviderEvent);
            migrationBuilder.DropColumn(name: "SubscriptionId", table: TableNames.PaymentProviderEvent);
            migrationBuilder.DropColumn(name: "InvoiceId", table: TableNames.PaymentProviderEvent);

            migrationBuilder.AlterColumn<Guid>(
                name: "CompanyId",
                table: TableNames.PaymentProviderEvent,
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ProcessedAt",
                table: TableNames.PaymentProviderEvent,
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_mp_TblPaymentProviderEvent_ExternalEventId",
                table: TableNames.PaymentProviderEvent);

            migrationBuilder.RenameColumn(
                name: "ExternalEventId",
                table: TableNames.PaymentProviderEvent,
                newName: "ProviderEventId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPaymentProviderEvent_ProviderEventId",
                table: TableNames.PaymentProviderEvent,
                column: "ProviderEventId",
                unique: true);
        }
    }
}
