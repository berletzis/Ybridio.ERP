-- ============================================================
-- ADR-042 — Commercial Discount Pattern
-- Agrega columna DescuentoPct a ventas.CotizacionDetalle
--
-- Ejecutar en: YBRIDIO-26 (base de datos de desarrollo)
-- Autor: Ybridio ERP
-- Fecha: 2026-05-12
--
-- Precondición: tabla ventas.CotizacionDetalle debe existir.
-- Postcondición: columna DescuentoPct NOT NULL DEFAULT 0,
--   todos los registros existentes quedan con 0% (sin descuento).
-- ============================================================

-- Idempotente: solo agrega si la columna no existe
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns c
    JOIN   sys.tables  t ON c.object_id = t.object_id
    JOIN   sys.schemas s ON t.schema_id = s.schema_id
    WHERE  s.name = 'ventas'
      AND  t.name = 'CotizacionDetalle'
      AND  c.name = 'DescuentoPct'
)
BEGIN
    ALTER TABLE ventas.CotizacionDetalle
        ADD DescuentoPct DECIMAL(5, 2) NOT NULL
            CONSTRAINT DF_CotizacionDetalle_DescuentoPct DEFAULT (0);

    PRINT 'OK: columna DescuentoPct agregada a ventas.CotizacionDetalle con DEFAULT 0.';
END
ELSE
BEGIN
    PRINT 'INFO: columna DescuentoPct ya existe — no se realizaron cambios.';
END
GO
