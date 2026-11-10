using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Backend.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueProductBarcodePerTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_Barcode",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Barcode",
                table: "Products",
                columns: new[] { "TenantId", "Barcode" },
                unique: true,
                filter: "[Barcode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_Barcode",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Barcode",
                table: "Products",
                columns: new[] { "TenantId", "Barcode" });
        }
    }
}
