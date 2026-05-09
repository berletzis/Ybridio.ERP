-- ============================================================
-- Finanzas Operativas PYME — DDL + Seed (2026-05-08)
-- Ejecutar contra BD YBRIDIO-26
-- ============================================================

USE [YBRIDIO-26];

-- ── 1. CategoriaFinanciera ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='CategoriaFinanciera' AND schema_id=SCHEMA_ID('finanzas'))
BEGIN
    CREATE TABLE [finanzas].[CategoriaFinanciera] (
        [Id]                     INT              IDENTITY(1,1) NOT NULL,
        [EmpresaId]              INT              NOT NULL,
        [TipoAplicable]          NVARCHAR(20)     NOT NULL DEFAULT 'Ambos',
        [Nombre]                 NVARCHAR(100)    NOT NULL,
        [Descripcion]            NVARCHAR(300)    NULL,
        [Color]                  NVARCHAR(20)     NULL,
        [Activo]                 BIT              NOT NULL DEFAULT 1,
        [FechaCreacion]          DATETIME         NOT NULL DEFAULT GETDATE(),
        [UsuarioCreacionId]      UNIQUEIDENTIFIER NOT NULL,
        [FechaModificacion]      DATETIME         NULL,
        [UsuarioModificacionId]  UNIQUEIDENTIFIER NULL,
        [Borrado]                BIT              NOT NULL DEFAULT 0,
        [RowVersion]             ROWVERSION       NOT NULL,
        CONSTRAINT [PK_CategoriaFinanciera] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CategoriaFinanciera_Empresa] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa]([Id])
    );
    CREATE INDEX [IX_CategoriaFinanciera_Empresa_Nombre] ON [finanzas].[CategoriaFinanciera]([EmpresaId],[Nombre]);
    PRINT 'Tabla finanzas.CategoriaFinanciera CREADA.';
END
ELSE PRINT 'finanzas.CategoriaFinanciera ya existe — omitiendo.';

-- ── 2. MovimientoFinanciero ───────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='MovimientoFinanciero' AND schema_id=SCHEMA_ID('finanzas'))
BEGIN
    CREATE TABLE [finanzas].[MovimientoFinanciero] (
        [Id]                     BIGINT           IDENTITY(1,1) NOT NULL,
        [EmpresaId]              INT              NOT NULL,
        [SucursalId]             INT              NULL,
        [Tipo]                   INT              NOT NULL,
        [Contexto]               INT              NOT NULL DEFAULT 0,
        [UsuarioContextoId]      UNIQUEIDENTIFIER NULL,
        [CategoriaId]            INT              NULL,
        [Concepto]               NVARCHAR(200)    NOT NULL,
        [Monto]                  DECIMAL(18,2)    NOT NULL,
        [Fecha]                  DATETIME         NOT NULL,
        [Observaciones]          NVARCHAR(500)    NULL,
        [FechaCreacion]          DATETIME         NOT NULL DEFAULT GETDATE(),
        [UsuarioCreacionId]      UNIQUEIDENTIFIER NOT NULL,
        [FechaModificacion]      DATETIME         NULL,
        [UsuarioModificacionId]  UNIQUEIDENTIFIER NULL,
        [Borrado]                BIT              NOT NULL DEFAULT 0,
        [RowVersion]             ROWVERSION       NOT NULL,
        CONSTRAINT [PK_MovimientoFinanciero]      PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MovFinanciero_Empresa]     FOREIGN KEY ([EmpresaId])   REFERENCES [core].[Empresa]([Id]),
        CONSTRAINT [FK_MovFinanciero_Sucursal]    FOREIGN KEY ([SucursalId])  REFERENCES [core].[Sucursal]([Id]),
        CONSTRAINT [FK_MovFinanciero_Categoria]   FOREIGN KEY ([CategoriaId]) REFERENCES [finanzas].[CategoriaFinanciera]([Id]) ON DELETE SET NULL
    );
    CREATE INDEX [IX_MovimientoFinanciero_Empresa_Tipo_Fecha]     ON [finanzas].[MovimientoFinanciero]([EmpresaId],[Tipo],[Fecha]);
    CREATE INDEX [IX_MovimientoFinanciero_Empresa_Sucursal_Fecha] ON [finanzas].[MovimientoFinanciero]([EmpresaId],[SucursalId],[Fecha]);
    PRINT 'Tabla finanzas.MovimientoFinanciero CREADA.';
END
ELSE PRINT 'finanzas.MovimientoFinanciero ya existe — omitiendo.';

-- ── 3. CuentaPorCobrar ────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='CuentaPorCobrar' AND schema_id=SCHEMA_ID('finanzas'))
BEGIN
    CREATE TABLE [finanzas].[CuentaPorCobrar] (
        [Id]                     BIGINT           IDENTITY(1,1) NOT NULL,
        [EmpresaId]              INT              NOT NULL,
        [SucursalId]             INT              NULL,
        [NombreDeudor]           NVARCHAR(200)    NOT NULL,
        [Concepto]               NVARCHAR(200)    NOT NULL,
        [MontoOriginal]          DECIMAL(18,2)    NOT NULL,
        [MontoPagado]            DECIMAL(18,2)    NOT NULL DEFAULT 0,
        [FechaEmision]           DATETIME         NOT NULL,
        [FechaVencimiento]       DATETIME         NOT NULL,
        [Observaciones]          NVARCHAR(500)    NULL,
        [FechaCreacion]          DATETIME         NOT NULL DEFAULT GETDATE(),
        [UsuarioCreacionId]      UNIQUEIDENTIFIER NOT NULL,
        [FechaModificacion]      DATETIME         NULL,
        [UsuarioModificacionId]  UNIQUEIDENTIFIER NULL,
        [Borrado]                BIT              NOT NULL DEFAULT 0,
        [RowVersion]             ROWVERSION       NOT NULL,
        CONSTRAINT [PK_CuentaPorCobrar] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CxC_Empresa]     FOREIGN KEY ([EmpresaId])  REFERENCES [core].[Empresa]([Id]),
        CONSTRAINT [FK_CxC_Sucursal]    FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal]([Id])
    );
    CREATE INDEX [IX_CuentaPorCobrar_Empresa_Vencimiento] ON [finanzas].[CuentaPorCobrar]([EmpresaId],[FechaVencimiento]);
    PRINT 'Tabla finanzas.CuentaPorCobrar CREADA.';
END
ELSE PRINT 'finanzas.CuentaPorCobrar ya existe — omitiendo.';

-- ── 4. CuentaPorPagar ─────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='CuentaPorPagar' AND schema_id=SCHEMA_ID('finanzas'))
BEGIN
    CREATE TABLE [finanzas].[CuentaPorPagar] (
        [Id]                     BIGINT           IDENTITY(1,1) NOT NULL,
        [EmpresaId]              INT              NOT NULL,
        [SucursalId]             INT              NULL,
        [NombreAcreedor]         NVARCHAR(200)    NOT NULL,
        [Concepto]               NVARCHAR(200)    NOT NULL,
        [MontoOriginal]          DECIMAL(18,2)    NOT NULL,
        [MontoPagado]            DECIMAL(18,2)    NOT NULL DEFAULT 0,
        [FechaEmision]           DATETIME         NOT NULL,
        [FechaVencimiento]       DATETIME         NOT NULL,
        [Observaciones]          NVARCHAR(500)    NULL,
        [FechaCreacion]          DATETIME         NOT NULL DEFAULT GETDATE(),
        [UsuarioCreacionId]      UNIQUEIDENTIFIER NOT NULL,
        [FechaModificacion]      DATETIME         NULL,
        [UsuarioModificacionId]  UNIQUEIDENTIFIER NULL,
        [Borrado]                BIT              NOT NULL DEFAULT 0,
        [RowVersion]             ROWVERSION       NOT NULL,
        CONSTRAINT [PK_CuentaPorPagar] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CxP_Empresa]    FOREIGN KEY ([EmpresaId])  REFERENCES [core].[Empresa]([Id]),
        CONSTRAINT [FK_CxP_Sucursal]   FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal]([Id])
    );
    CREATE INDEX [IX_CuentaPorPagar_Empresa_Vencimiento] ON [finanzas].[CuentaPorPagar]([EmpresaId],[FechaVencimiento]);
    PRINT 'Tabla finanzas.CuentaPorPagar CREADA.';
END
ELSE PRINT 'finanzas.CuentaPorPagar ya existe — omitiendo.';

-- ── 5. Módulos de Seguridad ───────────────────────────────────
DECLARE @maxOrden INT = (SELECT ISNULL(MAX(Orden),0) FROM [seguridad].[Modulo]);

IF NOT EXISTS (SELECT 1 FROM [seguridad].[Modulo] WHERE Clave='finanzas')
    INSERT INTO [seguridad].[Modulo](Nombre,Clave,Orden,FechaCreacion,Borrado)
    VALUES('Finanzas','finanzas',@maxOrden+1,GETDATE(),0);

SET @maxOrden = @maxOrden + 1;

IF NOT EXISTS (SELECT 1 FROM [seguridad].[Modulo] WHERE Clave='cxc')
    INSERT INTO [seguridad].[Modulo](Nombre,Clave,Orden,FechaCreacion,Borrado)
    VALUES('Cuentas por Cobrar','cxc',@maxOrden+1,GETDATE(),0);

SET @maxOrden = @maxOrden + 1;

IF NOT EXISTS (SELECT 1 FROM [seguridad].[Modulo] WHERE Clave='cxp')
    INSERT INTO [seguridad].[Modulo](Nombre,Clave,Orden,FechaCreacion,Borrado)
    VALUES('Cuentas por Pagar','cxp',@maxOrden+1,GETDATE(),0);

-- ── 6. Permisos ───────────────────────────────────────────────
DECLARE @modFinanzasId INT = (SELECT Id FROM [seguridad].[Modulo] WHERE Clave='finanzas');
DECLARE @modCxCId      INT = (SELECT Id FROM [seguridad].[Modulo] WHERE Clave='cxc');
DECLARE @modCxPId      INT = (SELECT Id FROM [seguridad].[Modulo] WHERE Clave='cxp');

INSERT INTO [seguridad].[Permiso](ModuloId,Nombre,Clave,FechaCreacion,Borrado)
SELECT @modFinanzasId,p.Nombre,p.Clave,GETDATE(),0 FROM (VALUES
    ('Ver finanzas',        'finanzas.ver'),
    ('Crear movimientos',   'finanzas.crear'),
    ('Editar movimientos',  'finanzas.editar'),
    ('Eliminar movimientos','finanzas.eliminar')
) p(Nombre,Clave) WHERE NOT EXISTS(SELECT 1 FROM [seguridad].[Permiso] WHERE Clave=p.Clave);

INSERT INTO [seguridad].[Permiso](ModuloId,Nombre,Clave,FechaCreacion,Borrado)
SELECT @modCxCId,p.Nombre,p.Clave,GETDATE(),0 FROM (VALUES
    ('Ver CxC',  'cxc.ver'),
    ('Crear CxC','cxc.crear'),
    ('Editar CxC','cxc.editar')
) p(Nombre,Clave) WHERE NOT EXISTS(SELECT 1 FROM [seguridad].[Permiso] WHERE Clave=p.Clave);

INSERT INTO [seguridad].[Permiso](ModuloId,Nombre,Clave,FechaCreacion,Borrado)
SELECT @modCxPId,p.Nombre,p.Clave,GETDATE(),0 FROM (VALUES
    ('Ver CxP',  'cxp.ver'),
    ('Crear CxP','cxp.crear'),
    ('Editar CxP','cxp.editar')
) p(Nombre,Clave) WHERE NOT EXISTS(SELECT 1 FROM [seguridad].[Permiso] WHERE Clave=p.Clave);

PRINT 'Permisos de Finanzas/CxC/CxP insertados.';

-- ── 7. Asignar permisos a roles ───────────────────────────────
DECLARE @superAdminId UNIQUEIDENTIFIER = (SELECT Id FROM [AspNetRoles] WHERE NormalizedName='SUPERADMIN');
DECLARE @adminId      UNIQUEIDENTIFIER = (SELECT Id FROM [AspNetRoles] WHERE NormalizedName='ADMINISTRADOREMPRESA');
DECLARE @gerenteId    UNIQUEIDENTIFIER = (SELECT Id FROM [AspNetRoles] WHERE NormalizedName='GERENTESUCURSAL');

-- SuperAdmin: todos
INSERT INTO [seguridad].[RolPermiso](RolId,PermisoId,Permitido)
SELECT @superAdminId,p.Id,1 FROM [seguridad].[Permiso] p
WHERE p.Clave IN('finanzas.ver','finanzas.crear','finanzas.editar','finanzas.eliminar',
                 'cxc.ver','cxc.crear','cxc.editar','cxp.ver','cxp.crear','cxp.editar')
  AND NOT EXISTS(SELECT 1 FROM [seguridad].[RolPermiso] WHERE RolId=@superAdminId AND PermisoId=p.Id);

-- AdministradorEmpresa: todos
INSERT INTO [seguridad].[RolPermiso](RolId,PermisoId,Permitido)
SELECT @adminId,p.Id,1 FROM [seguridad].[Permiso] p
WHERE p.Clave IN('finanzas.ver','finanzas.crear','finanzas.editar','finanzas.eliminar',
                 'cxc.ver','cxc.crear','cxc.editar','cxp.ver','cxp.crear','cxp.editar')
  AND NOT EXISTS(SELECT 1 FROM [seguridad].[RolPermiso] WHERE RolId=@adminId AND PermisoId=p.Id);

-- GerenteSucursal: ver + crear
INSERT INTO [seguridad].[RolPermiso](RolId,PermisoId,Permitido)
SELECT @gerenteId,p.Id,1 FROM [seguridad].[Permiso] p
WHERE p.Clave IN('finanzas.ver','finanzas.crear','cxc.ver','cxc.crear','cxp.ver','cxp.crear')
  AND NOT EXISTS(SELECT 1 FROM [seguridad].[RolPermiso] WHERE RolId=@gerenteId AND PermisoId=p.Id);

PRINT 'RolPermiso asignados.';

-- ── 8. Seed: Categorías para todas las empresas existentes ────
INSERT INTO [finanzas].[CategoriaFinanciera](EmpresaId,TipoAplicable,Nombre,Descripcion,Color,Activo,FechaCreacion,UsuarioCreacionId,Borrado)
SELECT e.Id,c.Tipo,c.Nombre,c.Detalle,c.Color,1,GETDATE(),'00000000-0000-0000-0000-000000000001',0
FROM [core].[Empresa] e
CROSS JOIN (VALUES
    ('Gasto',  'Servicios básicos', 'Agua, luz, gas, internet',             '#4A9EBF'),
    ('Gasto',  'Nómina',            'Sueldos y salarios',                   '#E8734A'),
    ('Gasto',  'Transporte',        'Gasolina, peaje, logística',           '#7BAE6B'),
    ('Gasto',  'Mantenimiento',     'Reparaciones e instalaciones',         '#C4A35A'),
    ('Gasto',  'Compras menores',   'Artículos de oficina y consumibles',   '#9B6BB5'),
    ('Gasto',  'Viáticos',          'Gastos de viaje y representación',     '#5A8FC4'),
    ('Ingreso','Ingreso extra',      'Ingresos no recurrentes',              '#4ABF8A'),
    ('Ingreso','Préstamo recibido',  'Financiamientos recibidos',            '#BF4A7B'),
    ('Ingreso','Inversión',          'Retorno de inversiones',               '#8ABF4A'),
    ('Ambos',  'Otros',             'Movimientos sin categoría específica', '#A0A0A0')
) c(Tipo,Nombre,Detalle,Color)
WHERE NOT EXISTS(
    SELECT 1 FROM [finanzas].[CategoriaFinanciera]
    WHERE EmpresaId=e.Id AND Nombre=c.Nombre
);

PRINT 'Categorías seed insertadas.';
PRINT 'FINANZAS OPERATIVAS — SQL COMPLETADO.';
