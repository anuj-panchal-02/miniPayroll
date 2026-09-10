using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mp_TblPlatformState",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPlatformState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mp_TblPlatformCity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mp_TblPlatformCity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mp_TblPlatformCity_mp_TblPlatformState_StateId",
                        column: x => x.StateId,
                        principalTable: "mp_TblPlatformState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPlatformCity_StateId_Name",
                table: "mp_TblPlatformCity",
                columns: new[] { "StateId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPlatformState_Code",
                table: "mp_TblPlatformState",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mp_TblPlatformState_Name",
                table: "mp_TblPlatformState",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mp_TblPlatformCity");

            migrationBuilder.DropTable(
                name: "mp_TblPlatformState");
        }
    }
}
