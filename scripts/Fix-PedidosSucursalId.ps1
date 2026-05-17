param()
$cs = "Server=132.148.74.136\ybridio;Database=YBRIDIO-26;User Id=sa;Password=U3xc3pt!0n!22;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
Write-Host "Conexion establecida a YBRIDIO-26"

function Exec($sql, $desc="") {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.ExecuteNonQuery() | Out-Null
    if ($desc) { Write-Host "  OK: $desc" }
}
function ScalarInt($sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    return [int]$cmd.ExecuteScalar()
}

Write-Host ""
Write-Host "=== PASO 1: Pedidos sin SucursalId ==="
$sinSucursal = ScalarInt "SELECT COUNT(*) FROM ventas.Pedido WHERE Borrado=0 AND (SucursalId IS NULL OR SucursalId=0)"
Write-Host "  Pedidos sin SucursalId: $sinSucursal"

if ($sinSucursal -gt 0) {
    $primeraSucursal = ScalarInt "SELECT TOP 1 Id FROM core.Sucursal WHERE Borrado=0 ORDER BY Id"
    Write-Host "  Asignando SucursalId=$primeraSucursal a pedidos sin sucursal..."
    Exec "UPDATE ventas.Pedido SET SucursalId=$primeraSucursal WHERE Borrado=0 AND (SucursalId IS NULL OR SucursalId=0)" "Pedidos actualizados"
}

Write-Host ""
Write-Host "=== PASO 2: Ventas sin SucursalId ==="
$ventasSinSuc = ScalarInt "SELECT COUNT(*) FROM ventas.Venta WHERE Borrado=0 AND NombreCliente IS NOT NULL AND (SucursalId IS NULL OR SucursalId=0)"
Write-Host "  Ventas sin SucursalId: $ventasSinSuc"

if ($ventasSinSuc -gt 0) {
    $primeraSucursal = ScalarInt "SELECT TOP 1 Id FROM core.Sucursal WHERE Borrado=0 ORDER BY Id"
    Exec "UPDATE ventas.Venta SET SucursalId=$primeraSucursal WHERE Borrado=0 AND NombreCliente IS NOT NULL AND (SucursalId IS NULL OR SucursalId=0)" "Ventas actualizadas"
}

Write-Host ""
Write-Host "=== PASO 3: Estado final ==="
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, NombreCliente, Estatus, SucursalId, Folio, PedidoId FROM ventas.Venta WHERE Borrado=0 AND NombreCliente IS NOT NULL ORDER BY Id DESC"
$r = $cmd.ExecuteReader()
Write-Host "  Id | Cliente | Estatus | SucursalId | Folio | PedidoId"
while ($r.Read()) {
    Write-Host "  $($r['Id']) | $($r['NombreCliente']) | $($r['Estatus']) | $($r['SucursalId']) | $($r['Folio']) | $($r['PedidoId'])"
}
$r.Close()

Write-Host ""
Write-Host "=== PASO 4: Pedidos con ventas ya generadas ==="
$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = @"
SELECT p.Id, p.NombreCliente, p.Estatus, p.SucursalId,
       COUNT(v.Id) as VentasGeneradas
FROM ventas.Pedido p
LEFT JOIN ventas.Venta v ON v.PedidoId=p.Id AND v.Borrado=0
WHERE p.Borrado=0
GROUP BY p.Id, p.NombreCliente, p.Estatus, p.SucursalId
ORDER BY p.Id DESC
"@
$r2 = $cmd2.ExecuteReader()
Write-Host "  PedidoId | Cliente | Estatus | SucursalId | VentasGeneradas"
while ($r2.Read()) {
    Write-Host "  $($r2['Id']) | $($r2['NombreCliente']) | $($r2['Estatus']) | $($r2['SucursalId']) | $($r2['VentasGeneradas'])"
}
$r2.Close()

$conn.Close()
Write-Host ""
Write-Host "Script completado."
