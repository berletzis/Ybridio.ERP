param()

$cs = "Server=132.148.74.136\ybridio;Database=YBRIDIO-26;User Id=sa;Password=U3xc3pt!0n!22;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
Write-Host "Conexion establecida a YBRIDIO-26"

function Exec {
    param([string]$sql, [string]$desc = "")
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.ExecuteNonQuery() | Out-Null
    if ($desc) { Write-Host "  OK: $desc" }
}

function ScalarInt {
    param([string]$sql)
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    return [int]$cmd.ExecuteScalar()
}

function ColExists {
    param([string]$schema, [string]$table, [string]$col)
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('$schema.$table') AND name = '$col'"
    return ([int]$cmd.ExecuteScalar()) -gt 0
}

Write-Host ""
Write-Host "=== PASO 1: Verificar columna EsPrincipal en inventario.Almacen ==="

if (-not (ColExists "inventario" "Almacen" "EsPrincipal")) {
    Exec @"
ALTER TABLE inventario.Almacen
ADD EsPrincipal bit NOT NULL DEFAULT 0
"@ "Columna EsPrincipal agregada a inventario.Almacen"
} else {
    Write-Host "  SKIP: EsPrincipal ya existe en inventario.Almacen"
}

Write-Host ""
Write-Host "=== PASO 2: Estado actual de almacenes ==="

$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT a.Id, a.Nombre, a.SucursalId, a.EsPrincipal, a.Activo, a.Borrado,
       s.Nombre as SucursalNombre
FROM inventario.Almacen a
LEFT JOIN core.Sucursal s ON s.Id = a.SucursalId
ORDER BY a.SucursalId, a.Id
"@
$reader = $cmd.ExecuteReader()
Write-Host "  Id | Nombre | SucursalId | SucursalNombre | EsPrincipal | Activo"
while ($reader.Read()) {
    $id   = $reader["Id"]
    $nom  = $reader["Nombre"]
    $sid  = $reader["SucursalId"]
    $snm  = if ($reader.IsDBNull($reader.GetOrdinal("SucursalNombre"))) { "NULL" } else { $reader["SucursalNombre"] }
    $prin = $reader["EsPrincipal"]
    $act  = $reader["Activo"]
    Write-Host "  $id | $nom | $sid | $snm | $prin | $act"
}
$reader.Close()

Write-Host ""
Write-Host "=== PASO 3: Marcar almacen principal por sucursal (idempotente) ==="

# Para cada sucursal que NO tenga ningun almacen marcado como principal,
# marcar como principal el almacen activo con Id menor (el primero creado).
# Es idempotente: si ya hay uno marcado, no hace nada en esa sucursal.
Exec @"
UPDATE a
SET a.EsPrincipal = 1
FROM inventario.Almacen a
INNER JOIN (
    SELECT SucursalId, MIN(Id) as PrimerId
    FROM inventario.Almacen
    WHERE Borrado = 0 AND Activo = 1
      AND SucursalId IS NOT NULL
      AND SucursalId NOT IN (
          SELECT DISTINCT SucursalId
          FROM inventario.Almacen
          WHERE EsPrincipal = 1 AND Borrado = 0
            AND SucursalId IS NOT NULL
      )
    GROUP BY SucursalId
) candidatos ON a.Id = candidatos.PrimerId
"@ "Almacen principal marcado para sucursales sin principal"

Write-Host ""
Write-Host "=== PASO 4: Verificar resultado ==="

$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = @"
SELECT a.Id, a.Nombre, a.SucursalId, a.EsPrincipal,
       s.Nombre as SucursalNombre
FROM inventario.Almacen a
LEFT JOIN core.Sucursal s ON s.Id = a.SucursalId
WHERE a.Borrado = 0
ORDER BY a.SucursalId, a.Id
"@
$r2 = $cmd2.ExecuteReader()
Write-Host "  Id | Almacen | SucursalId | Sucursal | Principal"
while ($r2.Read()) {
    $id   = $r2["Id"]
    $nom  = $r2["Nombre"]
    $sid  = $r2["SucursalId"]
    $snm  = if ($r2.IsDBNull($r2.GetOrdinal("SucursalNombre"))) { "NULL" } else { $r2["SucursalNombre"] }
    $prin = $r2["EsPrincipal"]
    Write-Host "  $id | $nom | $sid | $snm | $prin"
}
$r2.Close()

Write-Host ""
Write-Host "=== PASO 5: Verificar sucursales SIN almacen principal ==="

$sinPrincipal = ScalarInt @"
SELECT COUNT(DISTINCT s.Id)
FROM core.Sucursal s
WHERE s.Borrado = 0
  AND NOT EXISTS (
      SELECT 1 FROM inventario.Almacen a
      WHERE a.SucursalId = s.Id AND a.EsPrincipal = 1 AND a.Borrado = 0
  )
  AND EXISTS (
      SELECT 1 FROM inventario.Almacen a2
      WHERE a2.SucursalId = s.Id AND a2.Borrado = 0
  )
"@

if ($sinPrincipal -gt 0) {
    Write-Host "  ADVERTENCIA: $sinPrincipal sucursal(es) tienen almacenes pero ninguno marcado como principal."
    Write-Host "  Ir a Configuracion > Sucursal > Almacenes y marcar uno como principal."
} else {
    Write-Host "  OK: Todas las sucursales con almacenes tienen un principal marcado."
}

Write-Host ""
Write-Host "=== PASO 6: Verificar UsuarioSucursal del usuario activo ==="

$cmd3 = $conn.CreateCommand()
$cmd3.CommandText = @"
SELECT u.UserName, u.Email,
       s.Nombre as Sucursal,
       us.SucursalId
FROM seguridad.Usuario u
LEFT JOIN seguridad.UsuarioSucursal us ON us.UsuarioId = u.Id
LEFT JOIN core.Sucursal s ON s.Id = us.SucursalId
WHERE u.Borrado = 0
ORDER BY u.UserName
"@
$r3 = $cmd3.ExecuteReader()
Write-Host "  Usuario | Email | SucursalAsignada"
while ($r3.Read()) {
    $usr = $r3["UserName"]
    $eml = $r3["Email"]
    $suc = if ($r3.IsDBNull($r3.GetOrdinal("Sucursal"))) { "SIN SUCURSAL" } else { $r3["Sucursal"] }
    Write-Host "  $usr | $eml | $suc"
}
$r3.Close()

Write-Host ""
Write-Host "=== PASO 7: Verificar UsuarioAlmacen ==="

$cmd4 = $conn.CreateCommand()
$cmd4.CommandText = @"
SELECT u.UserName,
       a.Nombre as Almacen,
       ua.AlmacenId
FROM seguridad.Usuario u
LEFT JOIN seguridad.UsuarioAlmacen ua ON ua.UsuarioId = u.Id
LEFT JOIN inventario.Almacen a ON a.Id = ua.AlmacenId
WHERE u.Borrado = 0
ORDER BY u.UserName
"@
$r4 = $cmd4.ExecuteReader()
Write-Host "  Usuario | AlmacenAsignado"
while ($r4.Read()) {
    $usr = $r4["UserName"]
    $alm = if ($r4.IsDBNull($r4.GetOrdinal("Almacen"))) { "Sin restriccion de almacen" } else { $r4["Almacen"] }
    Write-Host "  $usr | $alm"
}
$r4.Close()

Write-Host ""
Write-Host "=== PASO 8: Asignar sucursal activa a usuarios sin sucursal (si los hay) ==="

# Obtener el Id de la primera sucursal activa de la empresa
$primeraSucursal = ScalarInt @"
SELECT TOP 1 Id FROM core.Sucursal
WHERE Borrado = 0
ORDER BY Id
"@

if ($primeraSucursal -eq 0) {
    Write-Host "  ERROR: No hay sucursales en la BD. Crear una sucursal primero."
} else {
    Write-Host "  Sucursal por defecto a asignar: Id=$primeraSucursal"

    # Insertar UsuarioSucursal para usuarios que no tienen ninguna asignada
    $insertados = ScalarInt @"
INSERT INTO seguridad.UsuarioSucursal (UsuarioId, SucursalId)
SELECT u.Id, $primeraSucursal
FROM seguridad.Usuario u
WHERE u.Borrado = 0
  AND NOT EXISTS (
      SELECT 1 FROM seguridad.UsuarioSucursal us
      WHERE us.UsuarioId = u.Id
  )
SELECT @@ROWCOUNT
"@
    # Nota: El SELECT @@ROWCOUNT despues del INSERT no funciona directo — usar OUTPUT
    Write-Host "  Usuarios sin sucursal procesados. Verificar resultado abajo."
}

Write-Host ""
Write-Host "=== VERIFICACION FINAL ==="

$cmd5 = $conn.CreateCommand()
$cmd5.CommandText = @"
SELECT u.UserName,
       STRING_AGG(s.Nombre, ', ') as Sucursales
FROM seguridad.Usuario u
LEFT JOIN seguridad.UsuarioSucursal us ON us.UsuarioId = u.Id
LEFT JOIN core.Sucursal s ON s.Id = us.SucursalId
WHERE u.Borrado = 0
GROUP BY u.Id, u.UserName
ORDER BY u.UserName
"@
$r5 = $cmd5.ExecuteReader()
Write-Host "  Usuario | Sucursales asignadas"
while ($r5.Read()) {
    $usr = $r5["UserName"]
    $suc = if ($r5.IsDBNull($r5.GetOrdinal("Sucursales"))) { "SIN SUCURSAL - ACCION REQUERIDA" } else { $r5["Sucursales"] }
    Write-Host "  $usr | $suc"
}
$r5.Close()

$conn.Close()
Write-Host ""
Write-Host "Script completado."
Write-Host "Si algun usuario aparece SIN SUCURSAL, asignarlo manualmente o usar la UI de Configuracion > Sucursal > Permisos."
