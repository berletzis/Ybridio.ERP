SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- EvolveProductoTipoAndCotizacion_V1.sql
-- Product Type Classification Pattern + Commercial Charges Pattern
--
-- 1. Agrega catalogos.TipoProducto.Clave + OrdenVisual
-- 2. Agrega ventas.CotizacionDetalle.IvaAplicable
-- 3. Crea ventas.CotizacionCargo (cargos accesorios documentales)
--
-- Idempotente. BD destino: YBRIDIO-26 | Fecha: 2026-05-13
-- ══════════════════════════════════════════════════════════════════════════════

-- ── 1. TipoProducto.Clave ───────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='catalogos' AND TABLE_NAME='TipoProducto' AND COLUMN_NAME='Clave')
BEGIN
    ALTER TABLE catalogos.TipoProducto ADD Clave NVARCHAR(10) NOT NULL DEFAULT '';
    PRINT 'Columna Clave agregada a catalogos.TipoProducto.';
END
ELSE PRINT 'Columna Clave ya existe en TipoProducto.';
GO

-- ── 2. TipoProducto.OrdenVisual ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='catalogos' AND TABLE_NAME='TipoProducto' AND COLUMN_NAME='OrdenVisual')
BEGIN
    ALTER TABLE catalogos.TipoProducto ADD OrdenVisual INT NOT NULL DEFAULT 0;
    PRINT 'Columna OrdenVisual agregada a catalogos.TipoProducto.';
END
ELSE PRINT 'Columna OrdenVisual ya existe en TipoProducto.';
GO

-- Índice único filtrado para Clave
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_TipoProducto_EmpresaClave')
BEGIN
    CREATE UNIQUE INDEX UQ_TipoProducto_EmpresaClave
        ON catalogos.TipoProducto (EmpresaId, Clave)
        WHERE Clave != '';
    PRINT 'Índice UQ_TipoProducto_EmpresaClave creado.';
END
GO

-- ── 3. CotizacionDetalle.IvaAplicable ───────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='CotizacionDetalle' AND COLUMN_NAME='IvaAplicable')
BEGIN
    ALTER TABLE ventas.CotizacionDetalle ADD IvaAplicable BIT NOT NULL DEFAULT 1;
    PRINT 'Columna IvaAplicable agregada a ventas.CotizacionDetalle.';
END
ELSE PRINT 'Columna IvaAplicable ya existe en CotizacionDetalle.';
GO

-- ── 4. ventas.CotizacionCargo ───────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='ventas' AND TABLE_NAME='CotizacionCargo')
BEGIN
    CREATE TABLE ventas.CotizacionCargo (
        Id            BIGINT          NOT NULL IDENTITY(1,1),
        CotizacionId  BIGINT          NOT NULL,
        OtroCargoId   INT             NULL,
        Descripcion   NVARCHAR(200)   NOT NULL,
        Importe       DECIMAL(18,2)   NOT NULL,
        AplicaIva     BIT             NOT NULL DEFAULT 0,
        Orden         INT             NOT NULL DEFAULT 0,
        CONSTRAINT PK_CotizacionCargo PRIMARY KEY (Id),
        CONSTRAINT FK_CotizacionCargo_Cotizacion FOREIGN KEY (CotizacionId)
            REFERENCES ventas.Cotizacion(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CotizacionCargo_OtroCargo FOREIGN KEY (OtroCargoId)
            REFERENCES catalogos.OtroCargo(Id) ON DELETE SET NULL
    );
    CREATE INDEX IX_CotizacionCargo_CotizacionId ON ventas.CotizacionCargo (CotizacionId);
    PRINT 'Tabla ventas.CotizacionCargo creada.';
END
ELSE PRINT 'Tabla ventas.CotizacionCargo ya existe — omitida.';
GO

PRINT '== EvolveProductoTipoAndCotizacion_V1.sql completado ==';
GO
