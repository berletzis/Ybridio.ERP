SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- AddSerieDocumento_V1.sql
-- Shared Sequence/Folio Pattern — SerieDocumento
-- Idempotente. BD destino: YBRIDIO-26 | Fecha: 2026-05-13
-- ══════════════════════════════════════════════════════════════════════════════

-- ── 1. catalogos.SerieDocumento ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='catalogos' AND TABLE_NAME='SerieDocumento')
BEGIN
    CREATE TABLE catalogos.SerieDocumento (
        Id                    INT              NOT NULL IDENTITY(1,1),
        EmpresaId             INT              NOT NULL,
        SucursalId            INT              NULL,
        TipoDocumento         INT              NOT NULL,
        Prefijo               NVARCHAR(20)     NOT NULL,
        Longitud              INT              NOT NULL DEFAULT 6,
        SiguienteNumero       BIGINT           NOT NULL DEFAULT 1,
        ReinicioAnual         BIT              NOT NULL DEFAULT 0,
        AnioUltimoReinicio    INT              NULL,
        Activo                BIT              NOT NULL DEFAULT 1,
        FechaCreacion         DATETIME         NOT NULL DEFAULT GETDATE(),
        FechaModificacion     DATETIME         NULL,
        UsuarioCreacionId     UNIQUEIDENTIFIER NOT NULL,
        UsuarioModificacionId UNIQUEIDENTIFIER NULL,
        Borrado               BIT              NOT NULL DEFAULT 0,
        RowVersion            ROWVERSION       NOT NULL,
        CONSTRAINT PK_SerieDocumento  PRIMARY KEY (Id),
        CONSTRAINT FK_SerieDocumento_Empresa  FOREIGN KEY (EmpresaId)  REFERENCES core.Empresa(Id),
        CONSTRAINT FK_SerieDocumento_Sucursal FOREIGN KEY (SucursalId) REFERENCES core.Sucursal(Id)
    );
    CREATE UNIQUE INDEX UQ_SerieDocumento_Empresa_Tipo_Sucursal ON catalogos.SerieDocumento (EmpresaId, TipoDocumento, SucursalId);
    CREATE INDEX IX_SerieDocumento_EmpresaId ON catalogos.SerieDocumento (EmpresaId);
    PRINT 'Tabla catalogos.SerieDocumento creada.';
END
ELSE PRINT 'Tabla catalogos.SerieDocumento ya existe — omitida.';
GO

-- ── 2a. Columna Folio en ventas.Cotizacion ──────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Cotizacion' AND COLUMN_NAME='Folio')
BEGIN
    ALTER TABLE ventas.Cotizacion ADD Folio NVARCHAR(50) NULL;
    PRINT 'Columna Folio agregada a ventas.Cotizacion.';
END
ELSE PRINT 'Columna Folio ya existe en ventas.Cotizacion — omitida.';
GO

-- Índice filtrado separado (requiere GO previo para que la columna sea visible)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_Cotizacion_EmpresaFolio')
BEGIN
    CREATE UNIQUE INDEX UQ_Cotizacion_EmpresaFolio ON ventas.Cotizacion (EmpresaId, Folio) WHERE Folio IS NOT NULL;
    PRINT 'Índice UQ_Cotizacion_EmpresaFolio creado.';
END
GO

-- ── 2b. ventas.Pedido ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Pedido' AND COLUMN_NAME='Folio')
BEGIN
    ALTER TABLE ventas.Pedido ADD Folio NVARCHAR(50) NULL;
    PRINT 'Columna Folio agregada a ventas.Pedido.';
END
ELSE PRINT 'Columna Folio ya existe en ventas.Pedido — omitida.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_Pedido_EmpresaFolio')
BEGIN
    CREATE UNIQUE INDEX UQ_Pedido_EmpresaFolio ON ventas.Pedido (EmpresaId, Folio) WHERE Folio IS NOT NULL;
    PRINT 'Índice UQ_Pedido_EmpresaFolio creado.';
END
GO

-- ── 2c. ventas.Venta ────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='Venta' AND COLUMN_NAME='Folio')
BEGIN
    ALTER TABLE ventas.Venta ADD Folio NVARCHAR(50) NULL;
    PRINT 'Columna Folio agregada a ventas.Venta.';
END
ELSE PRINT 'Columna Folio ya existe en ventas.Venta — omitida.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_Venta_EmpresaFolio')
BEGIN
    CREATE UNIQUE INDEX UQ_Venta_EmpresaFolio ON ventas.Venta (EmpresaId, Folio) WHERE Folio IS NOT NULL;
    PRINT 'Índice UQ_Venta_EmpresaFolio creado.';
END
GO

-- ── 2d. ventas.OrdenTrabajo ─────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='OrdenTrabajo' AND COLUMN_NAME='Folio')
BEGIN
    ALTER TABLE ventas.OrdenTrabajo ADD Folio NVARCHAR(50) NULL;
    PRINT 'Columna Folio agregada a ventas.OrdenTrabajo.';
END
ELSE PRINT 'Columna Folio ya existe en ventas.OrdenTrabajo — omitida.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_OrdenTrabajo_EmpresaFolio')
BEGIN
    CREATE UNIQUE INDEX UQ_OrdenTrabajo_EmpresaFolio ON ventas.OrdenTrabajo (EmpresaId, Folio) WHERE Folio IS NOT NULL;
    PRINT 'Índice UQ_OrdenTrabajo_EmpresaFolio creado.';
END
GO

-- ── 2e. compras.OrdenCompra ─────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='compras' AND TABLE_NAME='OrdenCompra' AND COLUMN_NAME='Folio')
BEGIN
    ALTER TABLE compras.OrdenCompra ADD Folio NVARCHAR(50) NULL;
    PRINT 'Columna Folio agregada a compras.OrdenCompra.';
END
ELSE PRINT 'Columna Folio ya existe en compras.OrdenCompra — omitida.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_OrdenCompra_EmpresaFolio')
BEGIN
    CREATE UNIQUE INDEX UQ_OrdenCompra_EmpresaFolio ON compras.OrdenCompra (EmpresaId, Folio) WHERE Folio IS NOT NULL;
    PRINT 'Índice UQ_OrdenCompra_EmpresaFolio creado.';
END
GO

-- ── 3. Seed inicial SerieDocumento (COMENTADO — ajustar valores antes de ejecutar) ──
/*
DECLARE @Emp INT = 1;  -- Reemplazar con EmpresaId real
DECLARE @Usr UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';  -- Reemplazar con UserId real

IF NOT EXISTS (SELECT 1 FROM catalogos.SerieDocumento WHERE EmpresaId = @Emp)
    INSERT INTO catalogos.SerieDocumento (EmpresaId, SucursalId, TipoDocumento, Prefijo, Longitud, SiguienteNumero, ReinicioAnual, Activo, FechaCreacion, UsuarioCreacionId)
    VALUES
        (@Emp, NULL,  1, 'COT', 6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL,  2, 'PED', 6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL,  3, 'VTA', 6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL,  4, 'OT',  6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL,  5, 'ENT', 6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL,  6, 'SAL', 6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL,  7, 'OC',  6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL,  8, 'CNT', 6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL,  9, 'TRS', 6, 1, 0, 1, GETDATE(), @Usr),
        (@Emp, NULL, 10, 'AJU', 6, 1, 0, 1, GETDATE(), @Usr);
*/

PRINT '== AddSerieDocumento_V1.sql completado ==';
GO
