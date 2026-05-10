param()
$cs = "Server=132.148.74.136\ybridio;Database=YBRIDIO-26;User Id=sa;Password=U3xc3pt!0n!22;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
Write-Host "Conexion establecida a YBRIDIO-26"

function Exec {
	param([string]$sql)
	$cmd = $conn.CreateCommand()
	$cmd.CommandText = $sql
	$cmd.ExecuteNonQuery() | Out-Null
}

function ColExists {
	param([string]$schema, [string]$table, [string]$col)
	$cmd = $conn.CreateCommand()
	$cmd.CommandText = "SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('$schema.$table') AND name = '$col'"
	return ([int]$cmd.ExecuteScalar()) -gt 0
}

function FkExists {
	param([string]$name, [string]$table)
	$cmd = $conn.CreateCommand()
	$cmd.CommandText = "SELECT COUNT(1) FROM sys.foreign_keys WHERE name = '$name' AND parent_object_id = OBJECT_ID('$table')"
	return ([int]$cmd.ExecuteScalar()) -gt 0
}

function TableExists {
	param([string]$schema, [string]$table)
	$cmd = $conn.CreateCommand()
	$cmd.CommandText = "SELECT COUNT(1) FROM sys.tables WHERE schema_id = SCHEMA_ID('$schema') AND name = '$table'"
	return ([int]$cmd.ExecuteScalar()) -gt 0
}

function PermExists {
	param([string]$clave)
	$cmd = $conn.CreateCommand()
	$cmd.CommandText = "SELECT COUNT(1) FROM seguridad.Permiso WHERE Clave = '$clave'"
	return ([int]$cmd.ExecuteScalar()) -gt 0
}

# ── 1. Columnas documentales en ventas.Venta ──────────────────────────────
Write-Host "`n[PASO 1] Columnas en ventas.Venta..."

if (-not (ColExists "ventas" "Venta" "ClienteId")) {
	Exec "ALTER TABLE ventas.Venta ADD ClienteId INT NULL"
	Write-Host "  [CREADO] ClienteId INT NULL"
} else { Write-Host "  [--] ClienteId ya existe" }

if (-not (ColExists "ventas" "Venta" "NombreCliente")) {
	Exec "ALTER TABLE ventas.Venta ADD NombreCliente NVARCHAR(200) NULL"
	Write-Host "  [CREADO] NombreCliente NVARCHAR(200) NULL"
} else { Write-Host "  [--] NombreCliente ya existe" }

if (-not (ColExists "ventas" "Venta" "PedidoId")) {
	Exec "ALTER TABLE ventas.Venta ADD PedidoId BIGINT NULL"
	Write-Host "  [CREADO] PedidoId BIGINT NULL"
} else { Write-Host "  [--] PedidoId ya existe" }

if (-not (ColExists "ventas" "Venta" "Estatus")) {
	Exec "ALTER TABLE ventas.Venta ADD Estatus INT NOT NULL DEFAULT 1"
	Write-Host "  [CREADO] Estatus INT NOT NULL DEFAULT 1"
} else { Write-Host "  [--] Estatus ya existe" }

if (-not (ColExists "ventas" "Venta" "TipoPago")) {
	Exec "ALTER TABLE ventas.Venta ADD TipoPago INT NOT NULL DEFAULT 0"
	Write-Host "  [CREADO] TipoPago INT NOT NULL DEFAULT 0"
} else { Write-Host "  [--] TipoPago ya existe" }

if (-not (ColExists "ventas" "Venta" "Subtotal")) {
	Exec "ALTER TABLE ventas.Venta ADD Subtotal DECIMAL(18,2) NULL"
	Write-Host "  [CREADO] Subtotal DECIMAL(18,2) NULL"
} else { Write-Host "  [--] Subtotal ya existe" }

if (-not (ColExists "ventas" "Venta" "TotalPagado")) {
	Exec "ALTER TABLE ventas.Venta ADD TotalPagado DECIMAL(18,2) NOT NULL DEFAULT 0"
	Write-Host "  [CREADO] TotalPagado DECIMAL(18,2) NOT NULL DEFAULT 0"
} else { Write-Host "  [--] TotalPagado ya existe" }

if (-not (ColExists "ventas" "Venta" "Observaciones")) {
	Exec "ALTER TABLE ventas.Venta ADD Observaciones NVARCHAR(500) NULL"
	Write-Host "  [CREADO] Observaciones NVARCHAR(500) NULL"
} else { Write-Host "  [--] Observaciones ya existe" }

# FK a core.Cliente (tabla real identificada en BD)
if (-not (FkExists "FK_Venta_Cliente" "ventas.Venta")) {
	Exec "ALTER TABLE ventas.Venta ADD CONSTRAINT FK_Venta_Cliente FOREIGN KEY (ClienteId) REFERENCES core.Cliente(Id)"
	Write-Host "  [CREADO] FK_Venta_Cliente -> core.Cliente"
} else { Write-Host "  [--] FK_Venta_Cliente ya existe" }

Write-Host "[OK] ventas.Venta actualizada"

# ── 2. Tabla ventas.PagoVenta ──────────────────────────────────────────────
Write-Host "`n[PASO 2] Tabla ventas.PagoVenta..."

if (-not (TableExists "ventas" "PagoVenta")) {
	$ddl = @"
CREATE TABLE ventas.PagoVenta (
	Id                    BIGINT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
	VentaId               BIGINT           NOT NULL,
	Fecha                 DATETIME         NOT NULL,
	Monto                 DECIMAL(18,2)    NOT NULL,
	FormaPago             NVARCHAR(100)    NOT NULL DEFAULT 'Efectivo',
	Referencia            NVARCHAR(200)    NULL,
	FechaCreacion         DATETIME2        NOT NULL DEFAULT GETDATE(),
	FechaModificacion     DATETIME2        NULL,
	UsuarioCreacionId     UNIQUEIDENTIFIER NOT NULL,
	UsuarioModificacionId UNIQUEIDENTIFIER NULL,
	Borrado               BIT              NOT NULL DEFAULT 0,
	RowVersion            ROWVERSION       NOT NULL,
	CONSTRAINT FK_PagoVenta_Venta FOREIGN KEY (VentaId) REFERENCES ventas.Venta(Id)
)
"@
	Exec $ddl
	Exec "CREATE INDEX IX_PagoVenta_VentaId ON ventas.PagoVenta (VentaId)"
	Write-Host "  [CREADA] ventas.PagoVenta + IX_PagoVenta_VentaId"
} else {
	Write-Host "  [--] ventas.PagoVenta ya existe"
}

# ── 3. Permisos en seguridad.Permiso (ModuloId=1 = Ventas) ────────────────
Write-Host "`n[PASO 3] Permisos de Ventas (ModuloId=1)..."

if (-not (PermExists "venta.editar")) {
	Exec "INSERT INTO seguridad.Permiso (ModuloId, Nombre, Clave, Descripcion) VALUES (1, 'Editar venta', 'venta.editar', 'Modificar encabezado y detalles de una venta en Borrador')"
	Write-Host "  [CREADO] venta.editar"
} else { Write-Host "  [--] venta.editar ya existe" }

if (-not (PermExists "venta.confirmar")) {
	Exec "INSERT INTO seguridad.Permiso (ModuloId, Nombre, Clave, Descripcion) VALUES (1, 'Confirmar venta', 'venta.confirmar', 'Confirmar venta, descontar inventario, generar CxC si credito')"
	Write-Host "  [CREADO] venta.confirmar"
} else { Write-Host "  [--] venta.confirmar ya existe" }

if (-not (PermExists "pago.registrar")) {
	Exec "INSERT INTO seguridad.Permiso (ModuloId, Nombre, Clave, Descripcion) VALUES (1, 'Registrar pago', 'pago.registrar', 'Registrar pago parcial o total contra una venta')"
	Write-Host "  [CREADO] pago.registrar"
} else { Write-Host "  [--] pago.registrar ya existe" }

$conn.Close()
Write-Host "`n================================================"
Write-Host "Sales Transaction Layer aplicado correctamente."
Write-Host "================================================"
