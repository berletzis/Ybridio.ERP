using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ybridio.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración baseline vacía. La base de datos ya existía al introducir EF Migrations.
    /// Solo establece el punto de referencia en __EFMigrationsHistory; no crea ni modifica tablas.
    /// </summary>
    public partial class InitialSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
