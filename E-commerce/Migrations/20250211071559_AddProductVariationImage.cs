using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_commerce.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariationImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColorModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HexCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColorModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariationModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductId1 = table.Column<long>(type: "bigint", nullable: true),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    ColorId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariationModel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariationModel_ColorModel_ColorId",
                        column: x => x.ColorId,
                        principalTable: "ColorModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductVariationModel_MaterialModel_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "MaterialModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductVariationModel_Products_ProductId1",
                        column: x => x.ProductId1,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationModel_ColorId",
                table: "ProductVariationModel",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationModel_MaterialId",
                table: "ProductVariationModel",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariationModel_ProductId1",
                table: "ProductVariationModel",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_Warranties_ProductId",
                table: "Warranties",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyRequests_UserId",
                table: "WarrantyRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyRequests_WarrantyID",
                table: "WarrantyRequests",
                column: "WarrantyID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductVariationModel");


            migrationBuilder.DropTable(
                name: "ColorModel");

            migrationBuilder.DropTable(
                name: "MaterialModel");
        }
    }
}
