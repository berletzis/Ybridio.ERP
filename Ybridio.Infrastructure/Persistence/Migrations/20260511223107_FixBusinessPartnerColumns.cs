using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ybridio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixBusinessPartnerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── core.RelacionComercial ────────────────────────────────────────────────
            // Renombrar columna 'Tipo' → 'TipoRelacion'
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='RelacionComercial' AND COLUMN_NAME='Tipo') AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='RelacionComercial' AND COLUMN_NAME='TipoRelacion') EXEC sp_rename 'core.RelacionComercial.Tipo', 'TipoRelacion', 'COLUMN';");

            // Renombrar 'Notas' → 'Observaciones'
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='RelacionComercial' AND COLUMN_NAME='Notas') AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='RelacionComercial' AND COLUMN_NAME='Observaciones') EXEC sp_rename 'core.RelacionComercial.Notas', 'Observaciones', 'COLUMN';");

            // Agregar columna 'Activo'
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='RelacionComercial' AND COLUMN_NAME='Activo') ALTER TABLE [core].[RelacionComercial] ADD [Activo] bit NOT NULL DEFAULT 1;");

            // Actualizar índice de TipoRelacion
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='RelacionComercial' AND i.name='IX_RelacionComercial_EmpresaId_Tipo') DROP INDEX [IX_RelacionComercial_EmpresaId_Tipo] ON [core].[RelacionComercial];");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name='core' AND t.name='RelacionComercial' AND i.name='IX_RelacionComercial_EmpresaId_Tipo') CREATE INDEX [IX_RelacionComercial_EmpresaId_Tipo] ON [core].[RelacionComercial] ([EmpresaId], [TipoRelacion]);");

            // ── core.Persona ──────────────────────────────────────────────────────────
            // Agregar columna 'Apellidos'
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='Persona' AND COLUMN_NAME='Apellidos') ALTER TABLE [core].[Persona] ADD [Apellidos] nvarchar(200) NULL;");

            // Agregar columna 'Activo'
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='Persona' AND COLUMN_NAME='Activo') ALTER TABLE [core].[Persona] ADD [Activo] bit NOT NULL DEFAULT 1;");

            // ── core.EmpresaComercial ─────────────────────────────────────────────────
            // Renombrar 'Nombre' → 'RazonSocial'
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='EmpresaComercial' AND COLUMN_NAME='Nombre') AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='EmpresaComercial' AND COLUMN_NAME='RazonSocial') EXEC sp_rename 'core.EmpresaComercial.Nombre', 'RazonSocial', 'COLUMN';");

            // Agregar columna 'NombreComercial'
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='EmpresaComercial' AND COLUMN_NAME='NombreComercial') ALTER TABLE [core].[EmpresaComercial] ADD [NombreComercial] nvarchar(200) NULL;");

            // Agregar columna 'Activo'
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='EmpresaComercial' AND COLUMN_NAME='Activo') ALTER TABLE [core].[EmpresaComercial] ADD [Activo] bit NOT NULL DEFAULT 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='RelacionComercial' AND COLUMN_NAME='TipoRelacion') EXEC sp_rename 'core.RelacionComercial.TipoRelacion', 'Tipo', 'COLUMN';");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='RelacionComercial' AND COLUMN_NAME='Observaciones') EXEC sp_rename 'core.RelacionComercial.Observaciones', 'Notas', 'COLUMN';");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='RelacionComercial' AND COLUMN_NAME='Activo') ALTER TABLE [core].[RelacionComercial] DROP COLUMN [Activo];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='Persona' AND COLUMN_NAME='Apellidos') ALTER TABLE [core].[Persona] DROP COLUMN [Apellidos];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='Persona' AND COLUMN_NAME='Activo') ALTER TABLE [core].[Persona] DROP COLUMN [Activo];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='EmpresaComercial' AND COLUMN_NAME='RazonSocial') EXEC sp_rename 'core.EmpresaComercial.RazonSocial', 'Nombre', 'COLUMN';");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='EmpresaComercial' AND COLUMN_NAME='NombreComercial') ALTER TABLE [core].[EmpresaComercial] DROP COLUMN [NombreComercial];");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='core' AND TABLE_NAME='EmpresaComercial' AND COLUMN_NAME='Activo') ALTER TABLE [core].[EmpresaComercial] DROP COLUMN [Activo];");
        }
    }
}
