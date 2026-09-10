using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingPeriodSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mp_TblBillingPeriod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingPeriod = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    PricePerEmployee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BillableEmployees = table.Column<int>(type: "int", nullable: false),
                    BillableSource = table.Column<int>(type: "int", nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Prorated = table.Column<bool>(type: "bit", nullable: false),
                    DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblBillingPeriod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblBillingPeriod_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "mp_TblCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblBillingPeriod_mp_TblSubscription_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "mp_TblSubscription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblBillingPeriod_CompanyId_BillingPeriod",
                table: "mp_TblBillingPeriod",
                columns: new[] { "CompanyId", "BillingPeriod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblBillingPeriod_SubscriptionId",
                table: "mp_TblBillingPeriod",
                column: "SubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mp_TblBillingPeriod");
        }
    }
}
