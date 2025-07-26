using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace E_commerce.Migrations
{
    public partial class AddUserVoucher : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(nullable: true),
                    CouponCode = table.Column<string>(nullable: true),
                    ReceivedAt = table.Column<DateTime>(nullable: false),
                    IsUsed = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVouchers", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserVouchers");
        }
    }
}