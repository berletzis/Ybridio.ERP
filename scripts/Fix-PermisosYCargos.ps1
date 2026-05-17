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
Write-Host "=== PASO 1: Verificar permisos en BD ==="
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Clave, Nombre FROM seguridad.Permiso ORDER BY Clave"
$r = $cmd.ExecuteReader()
Write-Host "  Id | Clave | Nombre"
while ($r.Read()) { Write-Host "  $($r['Id']) | $($r['Clave']) | $($r['Nombre'])" }
$r.Close()

Write-Host ""
Write-Host "=== PASO 2: Verificar permisos criticos faltantes ==="
$claves = @(
    @{Clave="pago.registrar";  Nombre="Registrar pago"},
    @{Clave="venta.ver";       Nombre="Ver ventas"},
    @{Clave="venta.crear";     Nombre="Crear venta"},
    @{Clave="venta.editar";    Nombre="Editar venta"},
    @{Clave="venta.confirmar"; Nombre="Confirmar venta"},
    @{Clave="venta.cancelar";  Nombre="Cancelar venta"},
    @{Clave="pedido.ver";      Nombre="Ver pedidos"},
    @{Clave="pedido.crear";    Nombre="Crear pedido"},
    @{Clave="pedido.editar";   Nombre="Editar pedido"},
    @{Clave="pedido.cancelar"; Nombre="Cancelar pedido"},
    @{Clave="cotizacion.ver";  Nombre="Ver cotizaciones"},
    @{Clave="cotizacion.crear";Nombre="Crear cotizacion"},
    @{Clave="cotizacion.editar";Nombre="Editar cotizacion"}
)
$moduloVentasId = ScalarInt "SELECT TOP 1 Id FROM seguridad.Modulo WHERE Nombre LIKE '%Venta%' OR Nombre LIKE '%venta%' ORDER BY Id"
if ($moduloVentasId -eq 0) { $moduloVentasId = 1 }
Write-Host "  ModuloId Ventas: $moduloVentasId"

foreach ($p in $claves) {
    $existe = ScalarInt "SELECT COUNT(*) FROM seguridad.Permiso WHERE Clave='$($p.Clave)'"
    if ($existe -eq 0) {
        Exec "INSERT INTO seguridad.Permiso (ModuloId, Nombre, Clave) VALUES ($moduloVentasId, '$($p.Nombre)', '$($p.Clave)')" "Permiso creado: $($p.Clave)"
    } else {
        Write-Host "  SKIP: $($p.Clave) ya existe"
    }
}

Write-Host ""
Write-Host "=== PASO 3: Verificar roles y sus permisos ==="
$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = @"
SELECT r.Name as Rol, p.Clave, rp.Id as RolPermisoId
FROM seguridad.Rol r
LEFT JOIN seguridad.RolPermiso rp ON rp.RolId = r.Id
LEFT JOIN seguridad.Permiso p ON p.Id = rp.PermisoId
ORDER BY r.Name, p.Clave
"@
$r2 = $cmd2.ExecuteReader()
Write-Host "  Rol | Permiso | RolPermisoId"
while ($r2.Read()) {
    $rol = $r2['Rol']
    $clave = if ($r2.IsDBNull($r2.GetOrdinal("Clave"))) { "SIN PERMISOS" } else { $r2['Clave'] }
    $rpId = if ($r2.IsDBNull($r2.GetOrdinal("RolPermisoId"))) { "-" } else { $r2['RolPermisoId'] }
    Write-Host "  $rol | $clave | $rpId"
}
$r2.Close()

Write-Host ""
Write-Host "=== PASO 4: Asignar TODOS los permisos al rol Admin ==="
$adminRolId = ScalarInt "SELECT TOP 1 Id FROM seguridad.Rol WHERE Name IN ('Admin','Administrador','admin','SuperAdmin') ORDER BY Id"
if ($adminRolId -eq 0) {
    Write-Host "  ADVERTENCIA: No se encontro rol Admin. Roles disponibles:"
    $cmdRoles = $conn.CreateCommand()
    $cmdRoles.CommandText = "SELECT Id, Name FROM seguridad.Rol"
    $rRoles = $cmdRoles.ExecuteReader()
    while ($rRoles.Read()) { Write-Host "    Id=$($rRoles['Id']) | $($rRoles['Name'])" }
    $rRoles.Close()
    Write-Host "  Asignando a todos los roles..."
    $adminRolId = ScalarInt "SELECT TOP 1 Id FROM seguridad.Rol ORDER BY Id"
}
Write-Host "  RolId Admin: $adminRolId"

Exec @"
INSERT INTO seguridad.RolPermiso (RolId, PermisoId)
SELECT $adminRolId, p.Id
FROM seguridad.Permiso p
WHERE NOT EXISTS (
    SELECT 1 FROM seguridad.RolPermiso rp
    WHERE rp.RolId = $adminRolId AND rp.PermisoId = p.Id
)
"@ "Permisos faltantes asignados al rol Admin"

Write-Host ""
Write-Host "=== PASO 5: Verificar UsuarioPermiso override (si hay denegados explicitos) ==="
$cmd3 = $conn.CreateCommand()
$cmd3.CommandText = @"
SELECT u.UserName, p.Clave, up.Permitido
FROM seguridad.UsuarioPermiso up
JOIN seguridad.Usuario u ON u.Id = up.UsuarioId
JOIN seguridad.Permiso p ON p.Id = up.PermisoId
WHERE up.Permitido = 0
ORDER BY u.UserName, p.Clave
"@
$r3 = $cmd3.ExecuteReader()
$hayDenegados = $false
while ($r3.Read()) {
    if (-not $hayDenegados) {
        Write-Host "  ATENCION: Permisos denegados explicitamente (Permitido=0):"
        $hayDenegados = $true
    }
    Write-Host "  $($r3['UserName']) | $($r3['Clave']) | Permitido=$($r3['Permitido'])"
}
$r3.Close()
if (-not $hayDenegados) { Write-Host "  OK: Sin denegados explicitos" }

Write-Host ""
Write-Host "=== PASO 6: Invalidar cache de permisos ==="
Write-Host "  IMPORTANTE: Reinicia la aplicacion para que la cache de permisos se recargue."
Write-Host "  (MemoryPermissionCache TTL=10min - o reiniciar fuerza invalidacion inmediata)"

Write-Host ""
Write-Host "=== PASO 7: Estado de cargos en BD ==="
$cmd4 = $conn.CreateCommand()
$cmd4.CommandText = @"
SELECT pc.Id, pc.PedidoId, pc.Descripcion, pc.Importe, pc.AplicaIva, pc.Orden
FROM ventas.PedidoCargo pc
ORDER BY pc.Id DESC
"@
$r4 = $cmd4.ExecuteReader()
Write-Host "  Id | PedidoId | Descripcion | Importe | AplicaIva"
while ($r4.Read()) {
    Write-Host "  $($r4['Id']) | $($r4['PedidoId']) | '$($r4['Descripcion'])' | $($r4['Importe']) | $($r4['AplicaIva'])"
}
$r4.Close()

$conn.Close()
Write-Host ""
Write-Host "Script completado. Reinicia la app para que los cambios de permisos tomen efecto."
