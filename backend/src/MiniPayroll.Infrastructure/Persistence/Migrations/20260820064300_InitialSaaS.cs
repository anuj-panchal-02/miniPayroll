using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSaaS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mp_TblAuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblAuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblCompany",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSetupComplete = table.Column<bool>(type: "bit", nullable: false),
                    DailyRateMethod = table.Column<int>(type: "int", nullable: false),
                    WorkingDaysPerMonth = table.Column<int>(type: "int", nullable: false),
                    WeeklyOffDays = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblCompany", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblPlan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PricePerEmployee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DefaultEmployeeLimit = table.Column<int>(type: "int", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblRole",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblRole", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblUser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblUser_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "mp_TblCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblSubscription",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EmployeeLimit = table.Column<int>(type: "int", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    CurrentPeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblSubscription", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblSubscription_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "mp_TblCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblSubscription_mp_TblPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "mp_TblPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblRoleClaim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblRoleClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblRoleClaim_mp_TblRole_RoleId",
                        column: x => x.RoleId,
                        principalTable: "mp_TblRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblUserClaim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblUserClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblUserClaim_mp_TblUser_UserId",
                        column: x => x.UserId,
                        principalTable: "mp_TblUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblUserLogin",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblUserLogin", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_mp_TblUserLogin_mp_TblUser_UserId",
                        column: x => x.UserId,
                        principalTable: "mp_TblUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblUserRole",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblUserRole", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_mp_TblUserRole_mp_TblRole_RoleId",
                        column: x => x.RoleId,
                        principalTable: "mp_TblRole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mp_TblUserRole_mp_TblUser_UserId",
                        column: x => x.UserId,
                        principalTable: "mp_TblUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblUserToken",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblUserToken", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_mp_TblUserToken_mp_TblUser_UserId",
                        column: x => x.UserId,
                        principalTable: "mp_TblUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblPayment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceGstReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BillingPeriod = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPayment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPayment_mp_TblCompany_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "mp_TblCompany",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mp_TblPayment_mp_TblSubscription_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "mp_TblSubscription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblCompany_ContactEmail",
                table: "mp_TblCompany",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayment_CompanyId",
                table: "mp_TblPayment",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPayment_SubscriptionId",
                table: "mp_TblPayment",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPlan_Name",
                table: "mp_TblPlan",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "mp_TblRole",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblRoleClaim_RoleId",
                table: "mp_TblRoleClaim",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSubscription_CompanyId",
                table: "mp_TblSubscription",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblSubscription_PlanId",
                table: "mp_TblSubscription",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "mp_TblUser",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblUser_CompanyId",
                table: "mp_TblUser",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "mp_TblUser",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblUserClaim_UserId",
                table: "mp_TblUserClaim",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblUserLogin_UserId",
                table: "mp_TblUserLogin",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblUserRole_RoleId",
                table: "mp_TblUserRole",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mp_TblAuditLog");

            migrationBuilder.DropTable(
                name: "mp_TblPayment");

            migrationBuilder.DropTable(
                name: "mp_TblRoleClaim");

            migrationBuilder.DropTable(
                name: "mp_TblUserClaim");

            migrationBuilder.DropTable(
                name: "mp_TblUserLogin");

            migrationBuilder.DropTable(
                name: "mp_TblUserRole");

            migrationBuilder.DropTable(
                name: "mp_TblUserToken");

            migrationBuilder.DropTable(
                name: "mp_TblSubscription");

            migrationBuilder.DropTable(
                name: "mp_TblRole");

            migrationBuilder.DropTable(
                name: "mp_TblUser");

            migrationBuilder.DropTable(
                name: "mp_TblPlan");

            migrationBuilder.DropTable(
                name: "mp_TblCompany");
        }
    }
}
