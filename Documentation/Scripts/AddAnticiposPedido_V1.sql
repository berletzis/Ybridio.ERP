-- ============================================================
-- AddAnticiposPedido_V1.sql
-- Dimensión financiera en Pedidos + tabla AnticipoPedido
-- Ejecutar en: YBRIDIO-26
-- Idempotente: SÍ — verifica existencia antes de crear
-- ADR-065 / 2026-05-15
-- ============================================================

-- ── 1. Nuevas columnas en ventas.Pedido ───────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Pedido') AND name = 'AnticipoRequerido')
    ALTER TABLE ventas.Pedido ADD AnticipoRequerido DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Pedido') AND name = 'AnticipoPagado')
    ALTER TABLE ventas.Pedido ADD AnticipoPagado DECIMAL(18,2) NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Pedido') AND name = 'EstadoFinanciero')
    ALTER TABLE ventas.Pedido ADD EstadoFinanciero INT NOT NULL DEFAULT 0;

-- ── 2. Tabla ventas.AnticipoPedido ────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('ventas') AND name = 'AnticipoPedido')
BEGIN
    CREATE TABLE ventas.AnticipoPedido (
        Id                    BIGINT          IDENTITY(1,1) NOT NULL,
        PedidoId              BIGINT          NOT NULL,
        Fecha                 DATETIME2       NOT NULL DEFAULT GETDATE(),
        Monto                 DECIMAL(18,2)   NOT NULL,
        FormaPago             NVARCHAR(50)    NOT NULL DEFAULT 'Efectivo',
        Referencia            NVARCHAR(100)   NULL,
        -- AuditableEntity
        FechaCreacion         DATETIME2       NOT NULL DEFAULT GETDATE(),
        UsuarioCreacionId     UNIQUEIDENTIFIER NOT NULL,
        FechaModificacion     DATETIME2       NULL,
        UsuarioModificacionId UNIQUEIDENTIFIER NULL,
        Borrado               BIT             NOT NULL DEFAULT 0,
        RowVersion            ROWVERSION      NOT NULL,
        CONSTRAINT PK_AnticipoPedido PRIMARY KEY (Id),
        CONSTRAINT FK_AnticipoPedido_Pedido
            FOREIGN KEY (PedidoId) REFERENCES ventas.Pedido(Id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_AnticipoPedido_PedidoId
        ON ventas.AnticipoPedido(PedidoId);
END

PRINT 'AddAnticiposPedido_V1 aplicado correctamente.';
