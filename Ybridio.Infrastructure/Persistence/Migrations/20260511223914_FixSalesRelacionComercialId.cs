using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ybridio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSalesRelacionComercialId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ventas.Pedido ─────────────────────────────────────────────────────────
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Pedido_Cliente') ALTER TABLE [ventas].[Pedido] DROP CONSTRAINT [FK_Pedido_Cliente];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Pedido' AND COLUMN_NAME='ClienteId') AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Pedido' AND COLUMN_NAME='RelacionComercialId') EXEC sp_rename 'ventas.Pedido.ClienteId', 'RelacionComercialId', 'COLUMN';");
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Pedido_RelacionComercial')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Pedido' AND COLUMN_NAME='RelacionComercialId')
            AND NOT EXISTS (
                SELECT 1 FROM [ventas].[Pedido] p WHERE p.[RelacionComercialId] IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM [core].[RelacionComercial] rc WHERE rc.[Id] = p.[RelacionComercialId]))
            ALTER TABLE [ventas].[Pedido] ADD CONSTRAINT [FK_Pedido_RelacionComercial] FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial]([Id]);");
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='ventas' AND t.name='Pedido' AND i.name='IX_Pedido_RelacionComercialId')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Pedido' AND COLUMN_NAME='RelacionComercialId')
            CREATE INDEX [IX_Pedido_RelacionComercialId] ON [ventas].[Pedido] ([RelacionComercialId]);");

            // ── ventas.Cotizacion ─────────────────────────────────────────────────────
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Cotizacion_Cliente') ALTER TABLE [ventas].[Cotizacion] DROP CONSTRAINT [FK_Cotizacion_Cliente];");
            // Eliminar la columna ClienteId vieja si ya existe RelacionComercialId
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Cotizacion' AND COLUMN_NAME='ClienteId') AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Cotizacion' AND COLUMN_NAME='RelacionComercialId') ALTER TABLE [ventas].[Cotizacion] DROP COLUMN [ClienteId];");
            // Si no tiene RelacionComercialId todavía, renombrar
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Cotizacion' AND COLUMN_NAME='ClienteId') AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Cotizacion' AND COLUMN_NAME='RelacionComercialId') EXEC sp_rename 'ventas.Cotizacion.ClienteId', 'RelacionComercialId', 'COLUMN';");
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Cotizacion_RelacionComercial')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Cotizacion' AND COLUMN_NAME='RelacionComercialId')
            AND NOT EXISTS (
                SELECT 1 FROM [ventas].[Cotizacion] c WHERE c.[RelacionComercialId] IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM [core].[RelacionComercial] rc WHERE rc.[Id] = c.[RelacionComercialId]))
            ALTER TABLE [ventas].[Cotizacion] ADD CONSTRAINT [FK_Cotizacion_RelacionComercial] FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial]([Id]);");
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='ventas' AND t.name='Cotizacion' AND i.name='IX_Cotizacion_RelacionComercialId')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Cotizacion' AND COLUMN_NAME='RelacionComercialId')
            CREATE INDEX [IX_Cotizacion_RelacionComercialId] ON [ventas].[Cotizacion] ([RelacionComercialId]);");

            // ── ventas.OrdenTrabajo ───────────────────────────────────────────────────
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='OrdenTrabajo' AND COLUMN_NAME='ClienteId') AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='OrdenTrabajo' AND COLUMN_NAME='RelacionComercialId') EXEC sp_rename 'ventas.OrdenTrabajo.ClienteId', 'RelacionComercialId', 'COLUMN';");
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_OrdenTrabajo_RelacionComercial')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='OrdenTrabajo' AND COLUMN_NAME='RelacionComercialId')
            AND NOT EXISTS (
                SELECT 1 FROM [ventas].[OrdenTrabajo] o WHERE o.[RelacionComercialId] IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM [core].[RelacionComercial] rc WHERE rc.[Id] = o.[RelacionComercialId]))
            ALTER TABLE [ventas].[OrdenTrabajo] ADD CONSTRAINT [FK_OrdenTrabajo_RelacionComercial] FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial]([Id]);");
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='ventas' AND t.name='OrdenTrabajo' AND i.name='IX_OrdenTrabajo_RelacionComercialId')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='OrdenTrabajo' AND COLUMN_NAME='RelacionComercialId')
            CREATE INDEX [IX_OrdenTrabajo_RelacionComercialId] ON [ventas].[OrdenTrabajo] ([RelacionComercialId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
