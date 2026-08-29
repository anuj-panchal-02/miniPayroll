using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniPayroll.Infrastructure.Persistence.Migrations
{
    public partial class AddEmployeeDraft : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "JoiningDate",
                table: "mp_TblEmployee",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<int>(
                name: "DraftStep",
                table: "mp_TblEmployee",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DraftStep", table: "mp_TblEmployee");
            migrationBuilder.AlterColumn<DateOnly>(
                name: "JoiningDate",
                table: "mp_TblEmployee",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
