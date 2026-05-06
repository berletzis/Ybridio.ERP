using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ybridio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductoTienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductoTienda",
                schema: "catalogos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    TiendaId   = table.Column<int>(type: "int", nullable: false),
                    PrecioOverride = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false,
                        defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoTienda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoTienda_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalSchema: "catalogos",
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductoTienda_Tienda_TiendaId",
                        column: x => x.TiendaId,
                        principalSchema: "core",
                        principalTable: "Tienda",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoTienda_ProductoId_TiendaId",
                schema: "catalogos",
                table: "ProductoTienda",
                columns: new[] { "ProductoId", "TiendaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProductoTienda", schema: "catalogos");
        }
    }
}
