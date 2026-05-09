# Runtime Security Enforcement Layer

> Implementado: 2026-05-08

## Objetivo

Primera capa de enforcement real de seguridad runtime sobre los módulos de Inventario (Productos, Entradas, Salidas, Existencias), utilizando el motor Security Foundation ya existente (`IErpAuthorizationService`, `ISecurityScopeResolver`, `PermisosClave`).

---

## Módulos Protegidos

| Módulo | Permiso Ver | Permiso Escribir | Scope |
|---|---|---|---|
| **Productos** | `producto.ver` (ViewModel) | `producto.crear`, `producto.editar`, `producto.eliminar` (Service) | Empresa (catálogo global) |
| **Entradas** | `entrada.ver` (Service + ViewModel) | — (pendiente) | Empresa + Sucursal |
| **Salidas** | `salida.ver` (Service + ViewModel) | `salida.autorizar` (Service) | Empresa + Sucursal |
| **Existencias** | `existencia.ver` (Service + ViewModel) | — | Empresa + Almacén |

---

## Arquitectura del Enforcement

### Capas de autorización (doble capa)

```
WinUI ViewModel
  ├─ PRE-CHECK: _auth.PuedeAsync(PermisosClave.X.Ver)
  │    → Si deniega: muestra mensaje, reporta en observabilidad, no llama al servicio
  │
  └─ Llama al Application Service
           ├─ Guard: _auth.PuedeAsync(PermisosClave.X.Y)
           │    → Si deniega: ServiceResult.Fail(..., ErrorCode.Unauthorized)
           └─ Scope: _scopeResolver.TieneAccesoSucursalAsync / TieneAccesoAlmacenAsync
                → Si deniega: ServiceResult.Fail(..., ErrorCode.Unauthorized)
```

La doble capa garantiza:
1. **UX inmediata** — ViewModel corta antes de la llamada al servicio (sin latencia)
2. **Defensa en profundidad** — Service verifica aunque se llame directamente

### Pattern de autorización en servicios

```csharp
// 1. Permiso (cached via MemoryPermissionCache TTL 10 min)
if (!await _auth.PuedeAsync(PermisosClave.Entrada.Ver, ct))
    return ServiceResult<T>.Fail("Sin permiso...", ErrorCode.Unauthorized);

// 2. Scope de sucursal
if (_session.UsuarioId is { } uid)
    if (!await _scopeResolver.TieneAccesoSucursalAsync(uid, sucursalId, ct))
        return ServiceResult<T>.Fail("Sin acceso a la sucursal.", ErrorCode.Unauthorized);
```

### Pattern de pre-check en ViewModels

```csharp
if (!await _auth.PuedeAsync(PermisosClave.Producto.Ver, ct))
{
    ErrorMessage = "Sin permiso para ver productos (producto.ver).";
    _observability.Report(BuildOperationalContext(elapsed, denied: true, permiso: PermisosClave.Producto.Ver));
    _contextTracker.SetViewModelContext(BuildCurrentContext(denied: true));
    return;
}
```

---

## Integración con Observabilidad Runtime

Cuando el acceso se deniega, el `GridOperationContext` incluye una nota explícita:

```
Notes: ["ACCESO DENEGADO — permiso requerido: producto.ver"]
```

El Runtime Diagnostic Panel muestra este estado en el tab **Operacional** y **Alertas**, permitiendo que desarrolladores y administradores vean:
- Qué permiso fue verificado
- Si fue permitido o denegado
- Qué scope fue aplicado

---

## Nuevos Servicios (Application Layer)

### `IEntradaService` / `EntradaService`
- `ListarAsync(empresaId, sucursalId, desde?, hasta?)` → `ServiceResult<IReadOnlyList<EntradaResumenDto>>`
- Valida: `entrada.ver` + scope sucursal

### `ISalidaService` / `SalidaService`
- `ListarAsync(empresaId, sucursalId, desde?, hasta?)` → `ServiceResult<IReadOnlyList<SalidaResumenDto>>`
- `AutorizarAsync(salidaId, usuarioId)` → `ServiceResult`
- Valida: `salida.ver` / `salida.autorizar` + scope sucursal

### `IInventarioService.ListarExistenciasSeguraAsync`
- Nuevo método en servicio existente
- Valida: `existencia.ver` + scope almacén (via `ISecurityScopeResolver.ObtenerAlmacentesPermitidosAsync`)
- Si usuario tiene almacenes restringidos → filtra automáticamente
- Si lista vacía (SuperAdmin o sin restricciones) → retorna todos los almacenes de la empresa

---

## Scopes Aplicados

### Existencias — Scope de Almacén (crítico)

```csharp
var almacenesPermitidos = await _scopeResolver.ObtenerAlmacentesPermitidosAsync(uid, ct);
if (almacenesPermitidos.Count > 0)
{
    // Filtra a los almacenes del usuario
    query = query.Where(e => almacenesPermitidos.Contains(e.AlmacenId));
}
// Lista vacía = SuperAdmin o sin restricciones → acceso total
```

### Entradas/Salidas — Scope de Sucursal

```csharp
if (!await _scopeResolver.TieneAccesoSucursalAsync(uid, sucursalId, ct))
    return ServiceResult<T>.Fail("Sin acceso a la sucursal indicada.", ErrorCode.Unauthorized);
```

### Productos — Solo Empresa (catálogo global)

Productos es un catálogo global por empresa — no tiene dimensión de sucursal ni almacén. El scope se aplica a nivel empresa por el filtro global del DbContext.

---

## Guards en ProductoService (operaciones de escritura)

| Operación | Permiso verificado |
|---|---|
| `CrearAsync` | `producto.crear` |
| `ActualizarAsync` | `producto.editar` |
| `ClonarAsync` | `producto.crear` |
| `CambiarActivoAsync` | `producto.editar` |
| `EliminarAsync` | `producto.eliminar` |

`ListarPorEmpresaAsync` y `BuscarAsync` mantienen su firma original (`IReadOnlyList<T>`) para compatibilidad con el módulo POS. El guard de `producto.ver` vive en el ViewModel.

---

## Nuevas Páginas

### ExistenciasPage
Implementada desde stub "Próximamente". Muestra existencias del inventario filtradas por scopes del usuario. StatusBar indica "Scope: almacenes autorizados" para que el usuario entienda que la vista está filtrada.

### EntradasPage / SalidasPage — DataTemplates
DataTemplates actualizados con `EntradaResumenDto` / `SalidaResumenDto`. Columnas: Folio | Fecha | Almacén | Concepto | Items | Estado.

---

## Restricciones Actuales (V1)

- Entradas: solo lectura (Crear/Editar/Eliminar son stubs pendientes)
- Salidas: solo lectura + autorizar (Crear/Editar/Eliminar son stubs pendientes)
- Existencias: solo lectura (sin CRUD de ajustes desde esta vista)
- `ListarPorEmpresaAsync` de Productos sin guard en servicio (guard en ViewModel)

---

## Pendientes Futuros

1. Guard `producto.ver` en `ProductoService.ListarPorEmpresaAsync` (requiere cambiar firma a `ServiceResult<T>`)
2. Servicios de creación/edición de Entradas y Salidas
3. Enforcement en módulo POS, Compras, Clientes, Proveedores
4. Ocultamiento visual de botones según permisos (siguiente fase)
5. UsuarioPermiso — UI para overrides individuales
