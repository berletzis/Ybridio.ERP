-- ============================================================
-- AddWorkflowColumns_V1.sql
-- Workflow Comercial Estabilización — 2026-05-14
--
-- Cambios:
--   1. ventas.PedidoDetalle — agrega DescuentoPct, IvaAplicable
--      (preserva descuentos e IVA en conversión COT→PED→VTA)
--   2. ventas.Pedido — agrega Subtotal nullable
--      (separa subtotal de total para futuros cargos accesorios)
--
-- Idempotente: usa IF NOT EXISTS para todas las operaciones.
-- Ejecutar en BD: YBRIDIO-26
-- ============================================================

-- ── 1. ventas.PedidoDetalle ──────────────────────────────────────────────────

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'ventas.PedidoDetalle')
      AND name = N'DescuentoPct'
)
BEGIN
    ALTER TABLE ventas.PedidoDetalle
        ADD DescuentoPct DECIMAL(5,2) NOT NULL DEFAULT 0;
    PRINT 'ventas.PedidoDetalle.DescuentoPct agregado.';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'ventas.PedidoDetalle')
      AND name = N'IvaAplicable'
)
BEGIN
    ALTER TABLE ventas.PedidoDetalle
        ADD IvaAplicable BIT NOT NULL DEFAULT 1;
    PRINT 'ventas.PedidoDetalle.IvaAplicable agregado.';
END

-- ── 2. ventas.Pedido ─────────────────────────────────────────────────────────

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'ventas.Pedido')
      AND name = N'Subtotal'
)
BEGIN
    ALTER TABLE ventas.Pedido
        ADD Subtotal DECIMAL(18,2) NULL;
    PRINT 'ventas.Pedido.Subtotal agregado.';
END

-- ── Post-migración: rellenar Subtotal = Total para registros existentes ──────

UPDATE ventas.Pedido
SET    Subtotal = Total
WHERE  Subtotal IS NULL;

PRINT 'Migración AddWorkflowColumns_V1 completada.';
