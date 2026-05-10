-- ============================================================
-- Sales Transaction Layer — DDL incremental para ventas.Venta y ventas.PagoVenta
-- Ejecutar con permisos DDL en la BD YbridioERP
-- NO usar EF migrations automáticas (ver ADR docs/DECISIONS.md)
-- ============================================================

-- ── 1. Columnas documentales en ventas.Venta ─────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Venta') AND name = 'ClienteId')
	ALTER TABLE ventas.Venta ADD ClienteId INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Venta') AND name = 'NombreCliente')
	ALTER TABLE ventas.Venta ADD NombreCliente NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Venta') AND name = 'PedidoId')
	ALTER TABLE ventas.Venta ADD PedidoId BIGINT NULL;

-- Estatus: POS legacy queda con 1 (Confirmada); nuevas ventas documentales inician en 0 (Borrador)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Venta') AND name = 'Estatus')
	ALTER TABLE ventas.Venta ADD Estatus INT NOT NULL DEFAULT 1;

-- TipoPago: 0 = Contado (default), 1 = Crédito
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Venta') AND name = 'TipoPago')
	ALTER TABLE ventas.Venta ADD TipoPago INT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Venta') AND name = 'Subtotal')
	ALTER TABLE ventas.Venta ADD Subtotal DECIMAL(18,2) NULL;

-- TotalPagado: acumula pagos; SaldoPendiente = Total - TotalPagado (calculado runtime)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Venta') AND name = 'TotalPagado')
	ALTER TABLE ventas.Venta ADD TotalPagado DECIMAL(18,2) NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ventas.Venta') AND name = 'Observaciones')
	ALTER TABLE ventas.Venta ADD Observaciones NVARCHAR(500) NULL;

-- FK opcional a core.Cliente (esquema real en YBRIDIO-26 es 'core', no 'catalogos')
IF NOT EXISTS (
	SELECT 1 FROM sys.foreign_keys
	WHERE name = 'FK_Venta_Cliente' AND parent_object_id = OBJECT_ID('ventas.Venta'))
	ALTER TABLE ventas.Venta
		ADD CONSTRAINT FK_Venta_Cliente
		FOREIGN KEY (ClienteId) REFERENCES core.Cliente(Id);

PRINT '[OK] ventas.Venta alterada con campos documentales';

-- ── 2. Tabla ventas.PagoVenta ─────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('ventas') AND name = 'PagoVenta')
BEGIN
	CREATE TABLE ventas.PagoVenta
	(
		Id                BIGINT          NOT NULL IDENTITY(1,1)  PRIMARY KEY,
		VentaId           BIGINT          NOT NULL,
		Fecha             DATETIME        NOT NULL,
		Monto             DECIMAL(18,2)   NOT NULL,
		FormaPago         NVARCHAR(100)   NOT NULL DEFAULT 'Efectivo',
		Referencia        NVARCHAR(200)   NULL,

		-- Auditoría (AuditableEntity)
		FechaCreacion     DATETIME        NOT NULL DEFAULT GETDATE(),
		FechaModificacion DATETIME        NULL,
		UsuarioCreacionId UNIQUEIDENTIFIER NOT NULL,
		UsuarioModificacionId UNIQUEIDENTIFIER NULL,
		Borrado           BIT             NOT NULL DEFAULT 0,
		RowVersion        ROWVERSION      NOT NULL,

		CONSTRAINT FK_PagoVenta_Venta
			FOREIGN KEY (VentaId) REFERENCES ventas.Venta(Id)
	);

	CREATE INDEX IX_PagoVenta_VentaId ON ventas.PagoVenta (VentaId);
	PRINT '[OK] ventas.PagoVenta creada';
END
ELSE
	PRINT '[OK] ventas.PagoVenta ya existe';

-- ── 3. Permisos seed: venta.editar, venta.confirmar, pago.registrar ──
-- ModuloId=1 = Ventas (verificado en BD YBRIDIO-26)
-- NOTA: seguridad.Permiso no tiene columna Descripcion — solo (ModuloId, Nombre, Clave)
IF NOT EXISTS (SELECT 1 FROM seguridad.Permiso WHERE Clave = 'venta.editar')
	INSERT INTO seguridad.Permiso (ModuloId, Nombre, Clave)
	VALUES (1, 'Editar venta', 'venta.editar');

IF NOT EXISTS (SELECT 1 FROM seguridad.Permiso WHERE Clave = 'venta.confirmar')
	INSERT INTO seguridad.Permiso (ModuloId, Nombre, Clave)
	VALUES (1, 'Confirmar venta', 'venta.confirmar');

IF NOT EXISTS (SELECT 1 FROM seguridad.Permiso WHERE Clave = 'pago.registrar')
	INSERT INTO seguridad.Permiso (ModuloId, Nombre, Clave)
	VALUES (1, 'Registrar pago', 'pago.registrar');

PRINT '[OK] Permisos venta.editar, venta.confirmar, pago.registrar verificados';
