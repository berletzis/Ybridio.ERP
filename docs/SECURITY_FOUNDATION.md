# Security Foundation Runtime Architecture

## Resumen

El motor de seguridad enterprise del ERP Ybridio implementa **RBAC + Profiles + Security Context Scopes**,
construido sobre ASP.NET Core Identity existente. Toda autorización se resuelve en runtime mediante DATA,
no código hardcodeado.

---

## Modelo conceptual

```
USUARIO ──→ ROL ──────────→ PERMISO
  │                           ↑
  └──→ PERFIL ────────────────┘
  │
  └──→ OVERRIDE (UsuarioPermiso)
  │
  └──→ SCOPE: Sucursal / Almacén
```

### Separación conceptual

| Concepto | Propósito |
|---|---|
| **Usuario** | Persona real autenticada |
| **Rol** | Función organizacional (Cajero, Vendedor, etc.) |
| **Perfil** | Conjunto reutilizable de permisos asignado directamente al usuario |
| **Permiso** | Acción granular de negocio (`venta.crear`, `salida.autorizar`) |
| **Security Scope** | Contexto permitido: empresa / sucursal / almacén |

---

## Evaluación de permisos (orden de prioridad)

```
1. UsuarioPermiso.Permitido = true/false  → override explícito (máxima prioridad)
   │  null = hereda hacia abajo
   │
2. UsuarioPerfil → PerfilPermiso          → perfiles asignados al usuario
   │
3. UsuarioRol → RolPermiso                → herencia desde roles
```

Un **denegado explícito** (`UsuarioPermiso.Permitido = false`) siempre gana sobre cualquier otro nivel.

---

## Entidades de dominio

### Entidades existentes (no modificadas)

| Entidad | Tabla | Descripción |
|---|---|---|
| `Modulo` | `seguridad.Modulo` | Agrupa permisos por área de negocio |
| `Permiso` | `seguridad.Permiso` | Acción granular: clave + módulo |
| `RolPermiso` | `seguridad.RolPermiso` | Rol → Permiso (bool Permitido) |
| `UsuarioPermiso` | `seguridad.UsuarioPermiso` | Override usuario (bool? null/true/false) |
| `UsuarioSucursal` | `seguridad.UsuarioSucursal` | Scope de sucursal del usuario |

### Entidades nuevas (Security Foundation)

| Entidad | Tabla | Descripción |
|---|---|---|
| `Perfil` | `seguridad.Perfil` | Perfil de permisos reutilizable |
| `PerfilPermiso` | `seguridad.PerfilPermiso` | Perfil ↔ Permiso (N:N, sin soft-delete) |
| `UsuarioPerfil` | `seguridad.UsuarioPerfil` | Usuario ↔ Perfil (N:N, asignación directa) |
| `UsuarioAlmacen` | `seguridad.UsuarioAlmacen` | Scope de almacén del usuario |

---

## Servicios de la Application Layer

### Existentes (extendidos)

| Servicio | Cambio |
|---|---|
| `IPermisoService` / `PermisoService` | Actualizado: incluye permisos de perfiles (nivel 2) |
| `IPermissionCache` / `MemoryPermissionCache` | Nueva implementación funcional (TTL 10 min, reemplaza NullPermissionCache) |

### Nuevos — Security Foundation

#### `IErpAuthorizationService`
Punto de entrada principal para decisiones de acceso.

```csharp
// Uso desde ViewModel o servicio:
var puede = await _auth.PuedeAsync(PermisosClave.Salida.Autorizar);
var permisos = await _auth.ObtenerPermisosEfectivosAsync();
var ctx = await _auth.ObtenerContextoSeguridad();
```

#### `ISecurityContextService`
Construye el `SecurityContextDto` completo: roles, perfiles, permisos efectivos y scopes.

#### `ISecurityScopeResolver`
Resuelve a qué sucursales y almacenes tiene acceso un usuario.

```csharp
// Verificar scope de sucursal
var acceso = await _scope.TieneAccesoSucursalAsync(userId, sucursalId);

// Lista de almacenes permitidos
var almacenes = await _scope.ObtenerAlmacentesPermitidosAsync(userId);
```

#### `IPerfilService`
Gestión CRUD de perfiles y asignación a usuarios.

---

## PermisosClave — catálogo de constantes

```csharp
// NO hardcodear strings. Usar siempre las constantes:
await _auth.PuedeAsync(PermisosClave.Salida.Autorizar);  // "salida.autorizar"
await _auth.PuedeAsync(PermisosClave.Venta.Crear);       // "venta.crear"
await _auth.PuedeAsync(PermisosClave.Caja.Abrir);        // "caja.abrir"
```

Categorías: `Venta`, `Entrada`, `Salida`, `Traspaso`, `Ajuste`, `Existencia`,
`Producto`, `Caja`, `Compra`, `Cliente`, `Proveedor`, `Configuracion`, `Seguridad`, `Reporte`.

---

## Security Context Scopes

### Jerarquía de scopes

```
Empresa
  └─ Sucursal (UsuarioSucursal)
       └─ Almacén (UsuarioAlmacen)
```

### Reglas de resolución (SecurityScopeResolver)

| Condición | Resultado |
|---|---|
| Usuario tiene rol `SuperAdmin` | Acceso total sin restricciones |
| Usuario tiene `UsuarioSucursal` asignadas | Solo esas sucursales |
| Usuario tiene `UsuarioAlmacen` asignados | Solo esos almacenes |
| Usuario tiene sucursales pero NO almacenes | Todos los almacenes de sus sucursales |

---

## ISessionContext — extensión

Se añadió `Guid? UsuarioId` a `ISessionContext` para que los servicios de Application puedan
acceder al usuario activo sin depender de WinUI.

```csharp
public interface ISessionContext
{
    int   EmpresaId  { get; }   // 0 = sin sesión / design-time
    int   SucursalId { get; }   // 0 = sin selección
    Guid? UsuarioId  { get; }   // null = no autenticado
}
```

---

## Runtime Observability — integración

El panel de diagnóstico (`RuntimeDiagnosticService`) incluye ahora `SecurityRuntimeSnapshot`
en el snapshot. Se provee mediante parámetro opcional para no ejecutar queries adicionales:

```csharp
// Con contexto de seguridad precalculado:
var secCtx = await _authService.ObtenerContextoSeguridad();
var snapshot = _diagnosticService.GetSnapshot(secCtx);

// Sin contexto (más ligero):
var snapshot = _diagnosticService.GetSnapshot();
```

Alertas de seguridad añadidas:
- Usuario sin permisos efectivos → Warning
- Modo SuperAdmin activo → Info
- Usuario sin roles asignados → Warning
- Contexto de seguridad no cargado → Info

---

## Datos seed iniciales

### Módulos (10)
ventas, inventario, caja, compras, productos, clientes, proveedores, configuracion, seguridad, reportes

### Permisos (51)
Ver, Crear, Cancelar/Autorizar/Aprobar por entidad de negocio. Claves en formato `entidad.accion`.

### Roles (8 nuevos + ADMIN existente)

| Rol | Permisos | Descripción |
|---|---|---|
| SuperAdmin | 51 (todos) | Acceso total sin restricciones de scope |
| AdministradorEmpresa | 50 | Todo menos editar configuración global |
| GerenteSucursal | 33 | Ventas + Inventario + Caja + Reportes |
| SupervisorInventario | 25 | Inventario completo + Compras + Productos |
| SupervisorVentas | 11 | Ventas completas + Clientes + Reportes |
| Cajero | 10 | Caja + Ventas básicas + Existencias |
| Vendedor | 7 | Ventas + Clientes + Consulta catálogo |
| OperadorInventario | 8 | Operaciones de almacén básicas |

### Perfiles (5)

| Perfil | Permisos | Uso |
|---|---|---|
| POS Básico | 10 | Punto de venta básico |
| Inventario Operativo | 8 | Operador de almacén |
| Inventario Supervisor | 25 | Supervisor de inventario |
| Ventas Operativo | 7 | Vendedor básico |
| Ventas Supervisor | 11 | Supervisor de ventas |

---

## Diagrama de flujo de autorización

```
Request: PuedeAsync("salida.autorizar")
    │
    ├─ ISessionContext.UsuarioId → obtiene usuario activo
    │
    ├─ IPermisoService.TienePermisoAsync(userId, "salida.autorizar")
    │       │
    │       ├─ 1. UsuarioPermiso.Permitido != null? → return directamente
    │       │
    │       ├─ 2. UsuarioPerfil → PerfilPermiso.Clave == "salida.autorizar"? → return true
    │       │
    │       └─ 3. UserManager.GetRolesAsync → RolPermiso.Clave == "salida.autorizar"? → return result
    │
    └─ bool: true (puede) / false (no puede)
```

---

## Decisiones de diseño importantes

1. **NO rehacer Identity**: se extiende el Identity existente (`ApplicationUser`, `ApplicationRole`).

2. **Permisos como DATA**: las claves viven en BD (`seguridad.Permiso`). `PermisosClave.cs` son solo constantes de referencia tipada.

3. **Perfil ≠ Rol**: los roles son asignaciones organizacionales; los perfiles son grupos de permisos reutilizables asignados directamente al usuario. Un cajero puede tener el rol `Cajero` Y el perfil `POS Básico`.

4. **MemoryPermissionCache**: reemplaza `NullPermissionCache`. TTL = 10 minutos. Para invalidar tras cambio de roles: llamar `IPermissionCache.InvalidateAsync(userId)`.

5. **SuperAdmin via Rol**: El rol `SuperAdmin` controla el bypass de scopes. No es un flag hardcodeado.

6. **`UsuarioAlmacen` vacío = sin restricción**: un usuario sin almacenes explícitos accede a todos los almacenes de sus sucursales asignadas.

7. **Runtime Enforcement activo** (V1): Productos, Entradas, Salidas, Existencias ya tienen guards. Ver `docs/RUNTIME_SECURITY_ENFORCEMENT.md`.

8. **CRITICAL: Usar `_context.Roles` en queries EF** — Nunca usar `_context.Set<IdentityRole<Guid>>()`. El DbContext registra `ApplicationRole` (tipo derivado), no el tipo base genérico. Ver ADR-014 en `docs/DECISIONS.md` y KI-012 en `docs/KNOWN_ISSUES.md`.

```csharp
// ✅ CORRECTO
_context.Roles.Where(r => rolesUsuario.Contains(r.Name!))

// ❌ INCORRECTO — causa InvalidOperationException en runtime
_context.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
```

7. **Runtime Enforcement activo** (V1): Productos, Entradas, Salidas, Existencias ya tienen guards de autorización. Ver `docs/RUNTIME_SECURITY_ENFORCEMENT.md` para el detalle completo.

---

## Security Administration Module (UI — implementado 2026-05-08)

La UI de administración del motor de seguridad vive en **Configuración Global → Seguridad**.

### Tabs disponibles

| Tab | Descripción |
|---|---|
| **Usuarios** | Grid de usuarios con roles y perfiles asignados. Botones: Asignar Roles, Asignar Perfiles, Asignar Scopes (ContentDialogs). |
| **Roles** | Grid de roles con conteo de permisos y usuarios. Botón: Asignar Permisos (checklist por módulo). |
| **Perfiles** | CRUD completo de perfiles. Botón: Administrar Permisos (checklist por módulo con pre-marcado). |
| **Permisos** | Solo lectura — catálogo completo de 51 permisos agrupados por módulo. |
| **Scopes** | Visualización y asignación de sucursales/almacenes por usuario. Doble clic para editar. |
| **Arquitectura Seguridad** | Guía técnica viva: flujo de autorización, implementación CORRECTO/INCORRECTO, Developer Guidance. |

### Servicio de administración

`ISecurityAdminService` / `SecurityAdminService` — servicio Application dedicado exclusivamente a las queries y operaciones de la UI de administración. **No modifica la lógica del motor de autorización runtime.**

Registro DI: `Scoped` en `ServiceCollectionExtensions.AddApplicationServices()`.

### Convención de diálogos

Todos los diálogos de asignación son `ContentDialog` inline en la Page (requieren `XamlRoot`). El ViewModel expone callbacks (`Action<T>?`) que la Page asigna en `OnNavigatedTo`. Esto mantiene el ViewModel desacoplado de WinUI.

---

## Pendientes futuros

- [ ] UI de administración de perfiles y asignación a usuarios
- [ ] UI de gestión de permisos por usuario (UsuarioPermiso overrides)
- [ ] Integración con enforcement visual (ocultar botones según `IErpAuthorizationService`)
- [ ] Caché distribuido (Redis) para `IPermissionCache` en multi-instancia
- [ ] Auditoría de cambios de seguridad (log de quién cambió qué permiso/rol)
- [ ] MFA (Multi-Factor Authentication)
- [ ] Policies basadas en claims de Identity
- [ ] `RolPerfil` (asignación de perfiles a roles para herencia automática)
