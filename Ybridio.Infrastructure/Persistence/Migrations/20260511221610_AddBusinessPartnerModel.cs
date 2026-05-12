using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ybridio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessPartnerModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Drop FK que aún exista ──────────────────────────────────────────────
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Factura_Cliente_ClienteId') ALTER TABLE [ventas].[Factura] DROP CONSTRAINT [FK_Factura_Cliente_ClienteId];");

            // ── 2. Renombrar columna ClienteId → RelacionComercialId en ventas.Factura ─
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Factura' AND COLUMN_NAME='ClienteId') AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Factura' AND COLUMN_NAME='RelacionComercialId') EXEC sp_rename 'ventas.Factura.ClienteId', 'RelacionComercialId', 'COLUMN';");

            // ── 3. Renombrar índice en ventas.Factura ──────────────────────────────────
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='ventas' AND t.name='Factura' AND i.name='IX_Factura_ClienteId') EXEC sp_rename N'ventas.Factura.IX_Factura_ClienteId', N'IX_Factura_RelacionComercialId', N'INDEX';");

            // ── 4. AlterColumn en UsuarioToken (reducir maxLength) ────────────────────
            migrationBuilder.Sql(@"IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.tables t ON c.object_id=t.object_id
                JOIN sys.schemas s ON t.schema_id=s.schema_id
                WHERE s.name='seguridad' AND t.name='UsuarioToken' AND c.name='Name' AND c.max_length>256)
            BEGIN
                ALTER TABLE [seguridad].[UsuarioToken] ALTER COLUMN [Name] nvarchar(128) NOT NULL;
            END");
            migrationBuilder.Sql(@"IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.tables t ON c.object_id=t.object_id
                JOIN sys.schemas s ON t.schema_id=s.schema_id
                WHERE s.name='seguridad' AND t.name='UsuarioToken' AND c.name='LoginProvider' AND c.max_length>256)
            BEGIN
                ALTER TABLE [seguridad].[UsuarioToken] ALTER COLUMN [LoginProvider] nvarchar(128) NOT NULL;
            END");

            // ── 5. Crear tablas nuevas solo si no existen ──────────────────────────────

            // core.EmpresaComercial
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='EmpresaComercial')
            CREATE TABLE [core].[EmpresaComercial] (
                [Id] int NOT NULL IDENTITY,
                [EmpresaId] int NOT NULL,
                [Nombre] nvarchar(200) NOT NULL,
                [RFC] nvarchar(20) NULL,
                [Email] nvarchar(200) NULL,
                [Telefono] nvarchar(30) NULL,
                [Direccion] nvarchar(300) NULL,
                [Notas] nvarchar(500) NULL,
                [FechaCreacion] datetime2 NOT NULL,
                [UsuarioCreacionId] uniqueidentifier NOT NULL,
                [FechaModificacion] datetime2 NULL,
                [UsuarioModificacionId] uniqueidentifier NULL,
                [Borrado] bit NOT NULL DEFAULT 0,
                [RowVersion] rowversion NOT NULL,
                CONSTRAINT [PK_EmpresaComercial] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_EmpresaComercial_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa]([Id]) ON DELETE CASCADE
            );");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='EmpresaComercial' AND i.name='IX_EmpresaComercial_EmpresaId')
            CREATE INDEX [IX_EmpresaComercial_EmpresaId] ON [core].[EmpresaComercial] ([EmpresaId]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='EmpresaComercial' AND i.name='IX_EmpresaComercial_EmpresaId_RFC')
            CREATE UNIQUE INDEX [IX_EmpresaComercial_EmpresaId_RFC] ON [core].[EmpresaComercial] ([EmpresaId], [RFC]) WHERE [RFC] IS NOT NULL;");

            // core.Persona
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='Persona')
            CREATE TABLE [core].[Persona] (
                [Id] int NOT NULL IDENTITY,
                [EmpresaId] int NOT NULL,
                [EmpresaComercialId] int NULL,
                [Nombre] nvarchar(200) NOT NULL,
                [RFC] nvarchar(20) NULL,
                [Email] nvarchar(200) NULL,
                [Telefono] nvarchar(30) NULL,
                [Direccion] nvarchar(300) NULL,
                [Notas] nvarchar(500) NULL,
                [FechaCreacion] datetime2 NOT NULL,
                [UsuarioCreacionId] uniqueidentifier NOT NULL,
                [FechaModificacion] datetime2 NULL,
                [UsuarioModificacionId] uniqueidentifier NULL,
                [Borrado] bit NOT NULL DEFAULT 0,
                [RowVersion] rowversion NOT NULL,
                CONSTRAINT [PK_Persona] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Persona_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa]([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_Persona_EmpresaComercial_EmpresaComercialId] FOREIGN KEY ([EmpresaComercialId]) REFERENCES [core].[EmpresaComercial]([Id])
            );");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='Persona' AND i.name='IX_Persona_EmpresaComercialId')
            CREATE INDEX [IX_Persona_EmpresaComercialId] ON [core].[Persona] ([EmpresaComercialId]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='Persona' AND i.name='IX_Persona_EmpresaId')
            CREATE INDEX [IX_Persona_EmpresaId] ON [core].[Persona] ([EmpresaId]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='Persona' AND i.name='IX_Persona_EmpresaId_RFC')
            CREATE UNIQUE INDEX [IX_Persona_EmpresaId_RFC] ON [core].[Persona] ([EmpresaId], [RFC]) WHERE [RFC] IS NOT NULL;");

            // core.RelacionComercial
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='RelacionComercial')
            CREATE TABLE [core].[RelacionComercial] (
                [Id] int NOT NULL IDENTITY,
                [EmpresaId] int NOT NULL,
                [Tipo] int NOT NULL,
                [EmpresaComercialId] int NULL,
                [PersonaId] int NULL,
                [LimiteCredito] decimal(18,2) NOT NULL DEFAULT 0,
                [Notas] nvarchar(500) NULL,
                [FechaCreacion] datetime2 NOT NULL,
                [UsuarioCreacionId] uniqueidentifier NOT NULL,
                [FechaModificacion] datetime2 NULL,
                [UsuarioModificacionId] uniqueidentifier NULL,
                [Borrado] bit NOT NULL DEFAULT 0,
                [RowVersion] rowversion NOT NULL,
                CONSTRAINT [PK_RelacionComercial] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_RelacionComercial_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa]([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_RelacionComercial_EmpresaComercial_EmpresaComercialId] FOREIGN KEY ([EmpresaComercialId]) REFERENCES [core].[EmpresaComercial]([Id]),
                CONSTRAINT [FK_RelacionComercial_Persona_PersonaId] FOREIGN KEY ([PersonaId]) REFERENCES [core].[Persona]([Id])
            );");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='RelacionComercial' AND i.name='IX_RelacionComercial_EmpresaComercialId')
            CREATE INDEX [IX_RelacionComercial_EmpresaComercialId] ON [core].[RelacionComercial] ([EmpresaComercialId]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='RelacionComercial' AND i.name='IX_RelacionComercial_EmpresaId_Tipo')
            CREATE INDEX [IX_RelacionComercial_EmpresaId_Tipo] ON [core].[RelacionComercial] ([EmpresaId], [Tipo]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='RelacionComercial' AND i.name='IX_RelacionComercial_PersonaId')
            CREATE INDEX [IX_RelacionComercial_PersonaId] ON [core].[RelacionComercial] ([PersonaId]);");

            // ── 6. FK de Factura a RelacionComercial ───────────────────────────────────
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Factura_RelacionComercial_RelacionComercialId')
            BEGIN
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Factura' AND COLUMN_NAME='RelacionComercialId')
                    ALTER TABLE [ventas].[Factura] ADD CONSTRAINT [FK_Factura_RelacionComercial_RelacionComercialId]
                        FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial]([Id]);
            END");

            // ── 7. Columna RelacionComercialId en ventas.Venta (si aún viene como ClienteId) ──
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Venta' AND COLUMN_NAME='RelacionComercialId')
            BEGIN
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Venta' AND COLUMN_NAME='ClienteId')
                    EXEC sp_rename 'ventas.Venta.ClienteId', 'RelacionComercialId', 'COLUMN';
                ELSE
                    ALTER TABLE [ventas].[Venta] ADD [RelacionComercialId] int NULL;
            END");

            // ── 8. FK Venta → RelacionComercial (solo si no hay datos huérfanos) ──────
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Venta_RelacionComercial')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Venta' AND COLUMN_NAME='RelacionComercialId')
            AND NOT EXISTS (
                SELECT 1 FROM [ventas].[Venta] v
                WHERE v.[RelacionComercialId] IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM [core].[RelacionComercial] rc WHERE rc.[Id] = v.[RelacionComercialId])
            )
            ALTER TABLE [ventas].[Venta] ADD CONSTRAINT [FK_Venta_RelacionComercial]
                FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial]([Id]);");

            // ── 9. Índices en ventas.Venta ─────────────────────────────────────────────
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='ventas' AND t.name='Venta' AND i.name='IX_Venta_RelacionComercialId')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Venta' AND COLUMN_NAME='RelacionComercialId')
            CREATE INDEX [IX_Venta_RelacionComercialId] ON [ventas].[Venta] ([RelacionComercialId]);");

            // ── 10. Índices en ventas.Factura ─────────────────────────────────────────
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='ventas' AND t.name='Factura' AND i.name='IX_Factura_RelacionComercialId')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Factura' AND COLUMN_NAME='RelacionComercialId')
            CREATE INDEX [IX_Factura_RelacionComercialId] ON [ventas].[Factura] ([RelacionComercialId]);");

            // ── 11. Índice único Almacen por Empresa+Sucursal ──────────────────────────
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='inventario' AND t.name='Almacen' AND i.name='IX_Almacen_EmpresaId_SucursalId')
            CREATE INDEX [IX_Almacen_EmpresaId_SucursalId] ON [inventario].[Almacen] ([EmpresaId], [SucursalId]);");

            // ── 12. FK Almacen → Sucursal ─────────────────────────────────────────────
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Almacen_Sucursal')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='inventario' AND TABLE_NAME='Almacen' AND COLUMN_NAME='SucursalId')
            ALTER TABLE [inventario].[Almacen] ADD CONSTRAINT [FK_Almacen_Sucursal]
                FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal]([Id]);");

            // ── 13. FK Caja → Sucursal ────────────────────────────────────────────────
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Caja_Sucursal_SucursalId')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='finanzas' AND TABLE_NAME='Caja' AND COLUMN_NAME='SucursalId')
            ALTER TABLE [finanzas].[Caja] ADD CONSTRAINT [FK_Caja_Sucursal_SucursalId]
                FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal]([Id]);");

            // ── 14. FK Venta → Sucursal ───────────────────────────────────────────────
            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Venta_Sucursal_SucursalId')
            AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Venta' AND COLUMN_NAME='SucursalId')
            ALTER TABLE [ventas].[Venta] ADD CONSTRAINT [FK_Venta_Sucursal_SucursalId]
                FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal]([Id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Almacen_Sucursal",
                schema: "inventario",
                table: "Almacen");

            migrationBuilder.DropForeignKey(
                name: "FK_Caja_Sucursal_SucursalId",
                schema: "finanzas",
                table: "Caja");

            migrationBuilder.DropForeignKey(
                name: "FK_Existencia_Sucursal",
                schema: "inventario",
                table: "Existencia");

            migrationBuilder.DropForeignKey(
                name: "FK_Factura_RelacionComercial_RelacionComercialId",
                schema: "ventas",
                table: "Factura");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientoInventario_Sucursal",
                schema: "inventario",
                table: "MovimientoInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_Venta_Cliente",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropForeignKey(
                name: "FK_Venta_Sucursal",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropTable(
                name: "AjusteInventarioDetalle",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "CotizacionDetalle",
                schema: "ventas");

            migrationBuilder.DropTable(
                name: "CuentaPorCobrar",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "CuentaPorPagar",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "MovimientoFinanciero",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "OrdenTrabajoMaterial",
                schema: "ventas");

            migrationBuilder.DropTable(
                name: "PagoVenta",
                schema: "ventas");

            migrationBuilder.DropTable(
                name: "PedidoDetalle",
                schema: "ventas");

            migrationBuilder.DropTable(
                name: "PerfilPermiso",
                schema: "seguridad");

            migrationBuilder.DropTable(
                name: "ProductoSucursal",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SalidaDetalle",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "Traspaso",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "UsuarioAlmacen",
                schema: "seguridad");

            migrationBuilder.DropTable(
                name: "UsuarioPerfil",
                schema: "seguridad");

            migrationBuilder.DropTable(
                name: "UsuarioSucursal",
                schema: "seguridad");

            migrationBuilder.DropTable(
                name: "AjusteInventario",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "CategoriaFinanciera",
                schema: "finanzas");

            migrationBuilder.DropTable(
                name: "OrdenTrabajo",
                schema: "ventas");

            migrationBuilder.DropTable(
                name: "EntradaDetalle",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "Perfil",
                schema: "seguridad");

            migrationBuilder.DropTable(
                name: "Pedido",
                schema: "ventas");

            migrationBuilder.DropTable(
                name: "Entrada",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "Cotizacion",
                schema: "ventas");

            migrationBuilder.DropTable(
                name: "ConceptoEntrada",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "EstatusEntrada",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "Salida",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "RelacionComercial",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ConceptoSalida",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "EstatusSalida",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "Sucursal",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Persona",
                schema: "core");

            migrationBuilder.DropTable(
                name: "EmpresaComercial",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "IX_Venta_RelacionComercialId",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropIndex(
                name: "IX_MovimientoInventario_SucursalId",
                schema: "inventario",
                table: "MovimientoInventario");

            migrationBuilder.DropIndex(
                name: "IX_Existencia_SucursalId",
                schema: "inventario",
                table: "Existencia");

            migrationBuilder.DropIndex(
                name: "IX_Almacen_EmpresaActivo",
                schema: "inventario",
                table: "Almacen");

            migrationBuilder.DropIndex(
                name: "UQ_Almacen_Sucursal_Codigo",
                schema: "inventario",
                table: "Almacen");

            migrationBuilder.DropColumn(
                name: "Estatus",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "NombreCliente",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "Observaciones",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "PedidoId",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "RelacionComercialId",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "TipoPago",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "TotalPagado",
                schema: "ventas",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                schema: "inventario",
                table: "TipoMovimientoInventario");

            migrationBuilder.DropColumn(
                name: "Signo",
                schema: "inventario",
                table: "TipoMovimientoInventario");

            migrationBuilder.DropColumn(
                name: "Folio",
                schema: "inventario",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "Observaciones",
                schema: "inventario",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "SaldoAcumulado",
                schema: "inventario",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                schema: "inventario",
                table: "MovimientoInventario");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                schema: "inventario",
                table: "Existencia");

            migrationBuilder.DropColumn(
                name: "Activo",
                schema: "inventario",
                table: "Almacen");

            migrationBuilder.DropColumn(
                name: "Codigo",
                schema: "inventario",
                table: "Almacen");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                schema: "inventario",
                table: "Almacen");

            migrationBuilder.DropColumn(
                name: "EsPrincipal",
                schema: "inventario",
                table: "Almacen");

            migrationBuilder.DropColumn(
                name: "Direccion",
                schema: "core",
                table: "Cliente");

            migrationBuilder.DropColumn(
                name: "LimiteCredito",
                schema: "core",
                table: "Cliente");

            migrationBuilder.DropColumn(
                name: "Notas",
                schema: "core",
                table: "Cliente");

            migrationBuilder.DropColumn(
                name: "Telefono",
                schema: "core",
                table: "Cliente");

            migrationBuilder.RenameTable(
                name: "Proveedor",
                schema: "core",
                newName: "Proveedor",
                newSchema: "catalogos");

            migrationBuilder.RenameTable(
                name: "ProductoCategoria",
                schema: "core",
                newName: "ProductoCategoria",
                newSchema: "catalogos");

            migrationBuilder.RenameTable(
                name: "Producto",
                schema: "core",
                newName: "Producto",
                newSchema: "catalogos");

            migrationBuilder.RenameTable(
                name: "Cliente",
                schema: "core",
                newName: "Cliente",
                newSchema: "catalogos");

            migrationBuilder.RenameColumn(
                name: "SucursalId",
                schema: "ventas",
                table: "Venta",
                newName: "TiendaId");

            migrationBuilder.RenameIndex(
                name: "IX_Venta_SucursalId",
                schema: "ventas",
                table: "Venta",
                newName: "IX_Venta_TiendaId");

            migrationBuilder.RenameIndex(
                name: "IX_Venta_EmpresaId_SucursalId_Fecha",
                schema: "ventas",
                table: "Venta",
                newName: "IX_Venta_EmpresaId_TiendaId_Fecha");

            migrationBuilder.RenameColumn(
                name: "RelacionComercialId",
                schema: "ventas",
                table: "Factura",
                newName: "ClienteId");

            migrationBuilder.RenameIndex(
                name: "IX_Factura_RelacionComercialId",
                schema: "ventas",
                table: "Factura",
                newName: "IX_Factura_ClienteId");

            migrationBuilder.RenameColumn(
                name: "SucursalId",
                schema: "finanzas",
                table: "Caja",
                newName: "TiendaId");

            migrationBuilder.RenameIndex(
                name: "IX_Caja_SucursalId",
                schema: "finanzas",
                table: "Caja",
                newName: "IX_Caja_TiendaId");

            migrationBuilder.RenameColumn(
                name: "SucursalId",
                schema: "inventario",
                table: "Almacen",
                newName: "TiendaId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "seguridad",
                table: "UsuarioToken",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "seguridad",
                table: "UsuarioToken",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ProviderKey",
                schema: "seguridad",
                table: "UsuarioLogin",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "seguridad",
                table: "UsuarioLogin",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.CreateTable(
                name: "Tienda",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Borrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()"),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UsuarioCreacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioModificacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tienda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tienda_Empresa",
                        column: x => x.EmpresaId,
                        principalSchema: "core",
                        principalTable: "Empresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoTienda",
                schema: "catalogos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    TiendaId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()"),
                    PrecioOverride = table.Column<decimal>(type: "decimal(18,6)", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "UsuarioTienda",
                schema: "seguridad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TiendaId = table.Column<int>(type: "int", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioTienda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioTienda_Tienda_TiendaId",
                        column: x => x.TiendaId,
                        principalSchema: "core",
                        principalTable: "Tienda",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UsuarioTienda_Usuario",
                        column: x => x.UsuarioId,
                        principalSchema: "seguridad",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Almacen_EmpresaId",
                schema: "inventario",
                table: "Almacen",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Almacen_TiendaId",
                schema: "inventario",
                table: "Almacen",
                column: "TiendaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoTienda_ProductoId_TiendaId",
                schema: "catalogos",
                table: "ProductoTienda",
                columns: new[] { "ProductoId", "TiendaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductoTienda_TiendaId",
                schema: "catalogos",
                table: "ProductoTienda",
                column: "TiendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tienda_EmpresaId",
                schema: "core",
                table: "Tienda",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioTienda_TiendaId",
                schema: "seguridad",
                table: "UsuarioTienda",
                column: "TiendaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioTienda_UsuarioId",
                schema: "seguridad",
                table: "UsuarioTienda",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Almacen_Tienda",
                schema: "inventario",
                table: "Almacen",
                column: "TiendaId",
                principalSchema: "core",
                principalTable: "Tienda",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Caja_Tienda_TiendaId",
                schema: "finanzas",
                table: "Caja",
                column: "TiendaId",
                principalSchema: "core",
                principalTable: "Tienda",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Factura_Cliente_ClienteId",
                schema: "ventas",
                table: "Factura",
                column: "ClienteId",
                principalSchema: "catalogos",
                principalTable: "Cliente",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Venta_Tienda",
                schema: "ventas",
                table: "Venta",
                column: "TiendaId",
                principalSchema: "core",
                principalTable: "Tienda",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
