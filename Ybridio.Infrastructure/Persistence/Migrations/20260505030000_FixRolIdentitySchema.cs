using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ybridio.Infrastructure.Persistence.Migrations;

/// <summary>
/// La tabla seguridad.Rol se creó como entidad de dominio antes de integrar ASP.NET Identity,
/// por lo que puede carecer de columnas requeridas por IdentityRole (Name, NormalizedName,
/// ConcurrencyStamp). Esta migración las añade de forma idempotente y crea la tabla completa
/// si no existe.
/// </summary>
public partial class FixRolIdentitySchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'seguridad' AND TABLE_NAME = 'Rol'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'seguridad')
        EXEC('CREATE SCHEMA seguridad');

    CREATE TABLE [seguridad].[Rol] (
        [Id]               [uniqueidentifier] NOT NULL DEFAULT NEWID(),
        [Name]             [nvarchar](256)    NULL,
        [NormalizedName]   [nvarchar](256)    NULL,
        [ConcurrencyStamp] [nvarchar](max)    NULL,
        [FechaCreacion]    [datetime2]        NOT NULL DEFAULT getdate(),
        [Borrado]          [bit]              NOT NULL DEFAULT 0,
        [RowVersion]       [rowversion]       NOT NULL,
        CONSTRAINT [PK_Rol] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [RoleNameIndex]
        ON [seguridad].[Rol] ([NormalizedName])
        WHERE [NormalizedName] IS NOT NULL;
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_SCHEMA='seguridad' AND TABLE_NAME='Rol' AND COLUMN_NAME='Name')
        ALTER TABLE [seguridad].[Rol] ADD [Name] [nvarchar](256) NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_SCHEMA='seguridad' AND TABLE_NAME='Rol' AND COLUMN_NAME='NormalizedName')
        ALTER TABLE [seguridad].[Rol] ADD [NormalizedName] [nvarchar](256) NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_SCHEMA='seguridad' AND TABLE_NAME='Rol' AND COLUMN_NAME='ConcurrencyStamp')
        ALTER TABLE [seguridad].[Rol] ADD [ConcurrencyStamp] [nvarchar](max) NULL;

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_SCHEMA='seguridad' AND TABLE_NAME='Rol' AND COLUMN_NAME='FechaCreacion')
        ALTER TABLE [seguridad].[Rol] ADD [FechaCreacion] [datetime2] NOT NULL DEFAULT getdate();

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_SCHEMA='seguridad' AND TABLE_NAME='Rol' AND COLUMN_NAME='Borrado')
        ALTER TABLE [seguridad].[Rol] ADD [Borrado] [bit] NOT NULL DEFAULT 0;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes i
        JOIN sys.objects o ON i.object_id = o.object_id
        JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE s.name = 'seguridad' AND o.name = 'Rol' AND i.name = 'RoleNameIndex'
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_SCHEMA='seguridad' AND TABLE_NAME='Rol' AND COLUMN_NAME='NormalizedName')
            CREATE UNIQUE INDEX [RoleNameIndex]
                ON [seguridad].[Rol] ([NormalizedName])
                WHERE [NormalizedName] IS NOT NULL;
    END
END
");
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
