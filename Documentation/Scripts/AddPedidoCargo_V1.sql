-- ============================================================
-- AddPedidoCargo_V1.sql
-- Commercial Charges Pattern para Pedidos — 2026-05-14
--
-- Crea tabla ventas.PedidoCargo equivalente a ventas.CotizacionCargo.
-- Los cargos accesorios (Flete, Maniobras, Seguro) se persisten por
-- separado de los detalles de producto.
-- Idempotente.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'ventas' AND t.name = 'PedidoCargo'
)
BEGIN
    CREATE TABLE ventas.PedidoCargo (
        Id          BIGINT          NOT NULL IDENTITY(1,1) CONSTRAINT PK_PedidoCargo PRIMARY KEY,
        PedidoId    BIGINT          NOT NULL,
        Descripcion NVARCHAR(200)   NOT NULL,
        Importe     DECIMAL(18,2)   NOT NULL,
        AplicaIva   BIT             NOT NULL DEFAULT 0,
        Orden       INT             NOT NULL DEFAULT 0,

        CONSTRAINT FK_PedidoCargo_Pedido_PedidoId
            FOREIGN KEY (PedidoId) REFERENCES ventas.Pedido(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_PedidoCargo_Pedido_Orden
        ON ventas.PedidoCargo (PedidoId, Orden);

    PRINT 'ventas.PedidoCargo creada.';
END
ELSE
    PRINT 'ventas.PedidoCargo ya existe.';
