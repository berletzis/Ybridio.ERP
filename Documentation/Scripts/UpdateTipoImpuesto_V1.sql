SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ══════════════════════════════════════════════════════════════════════════════
-- UpdateTipoImpuesto_V1.sql
-- Commercial Tax Pattern — Enriquecimiento del catálogo fiscal institucional.
--
-- Agrega a catalogos.TipoImpuesto:
--   Codigo        — código corto único (IVA16, IVA8, EXENTO)
--   TipoGravamen  — tipo de gravamen (1=IVA, 2=IEPS, 3=ISR, 4=Exento, 5=Otro)
--   EsExento      — flag explícito para queries de exención
--   Descripcion   — descripción técnica/legal
--   OrdenVisual   — orden en selectores
--
-- Además crea índice único filtrado para Codigo.
--
-- Idempotente. BD destino: YBRIDIO-26 | Fecha: 2026-05-13
-- ══════════════════════════════════════════════════════════════════════════════

-- ── Columna Codigo ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='catalogos' AND TABLE_NAME='TipoImpuesto' AND COLUMN_NAME='Codigo')
BEGIN
    ALTER TABLE catalogos.TipoImpuesto ADD Codigo NVARCHAR(20) NOT NULL DEFAULT '';
    PRINT 'Columna Codigo agregada a TipoImpuesto.';
END
ELSE PRINT 'Columna Codigo ya existe.';
GO

-- ── Columna TipoGravamen ────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='catalogos' AND TABLE_NAME='TipoImpuesto' AND COLUMN_NAME='TipoGravamen')
BEGIN
    ALTER TABLE catalogos.TipoImpuesto ADD TipoGravamen INT NOT NULL DEFAULT 1; -- 1=IVA
    PRINT 'Columna TipoGravamen agregada a TipoImpuesto.';
END
ELSE PRINT 'Columna TipoGravamen ya existe.';
GO

-- ── Columna EsExento ────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='catalogos' AND TABLE_NAME='TipoImpuesto' AND COLUMN_NAME='EsExento')
BEGIN
    ALTER TABLE catalogos.TipoImpuesto ADD EsExento BIT NOT NULL DEFAULT 0;
    PRINT 'Columna EsExento agregada a TipoImpuesto.';
END
ELSE PRINT 'Columna EsExento ya existe.';
GO

-- ── Columna Descripcion ─────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='catalogos' AND TABLE_NAME='TipoImpuesto' AND COLUMN_NAME='Descripcion')
BEGIN
    ALTER TABLE catalogos.TipoImpuesto ADD Descripcion NVARCHAR(500) NULL;
    PRINT 'Columna Descripcion agregada a TipoImpuesto.';
END
ELSE PRINT 'Columna Descripcion ya existe.';
GO

-- ── Columna OrdenVisual ─────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='catalogos' AND TABLE_NAME='TipoImpuesto' AND COLUMN_NAME='OrdenVisual')
BEGIN
    ALTER TABLE catalogos.TipoImpuesto ADD OrdenVisual INT NOT NULL DEFAULT 0;
    PRINT 'Columna OrdenVisual agregada a TipoImpuesto.';
END
ELSE PRINT 'Columna OrdenVisual ya existe.';
GO

-- ── Índice único filtrado para Codigo ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UQ_TipoImpuesto_EmpresaCodigo')
BEGIN
    CREATE UNIQUE INDEX UQ_TipoImpuesto_EmpresaCodigo
        ON catalogos.TipoImpuesto (EmpresaId, Codigo)
        WHERE Codigo != '';
    PRINT 'Índice UQ_TipoImpuesto_EmpresaCodigo creado.';
END
GO

-- ── Datos existentes: actualizar EsExento donde Porcentaje=0 ───────────────
UPDATE catalogos.TipoImpuesto
SET EsExento = 1, TipoGravamen = 4  -- 4=Exento
WHERE Porcentaje = 0 AND EsExento = 0 AND Borrado = 0;

PRINT 'Registros exentos existentes actualizados.';
GO

PRINT '== UpdateTipoImpuesto_V1.sql completado ==';
GO
