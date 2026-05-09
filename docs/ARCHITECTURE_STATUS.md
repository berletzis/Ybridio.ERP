# Architecture Status — Ybridio ERP

> Última actualización: 2026-05-08  
> Build: ✅ 0 errores | BD: YBRIDIO-26 | Docs relacionados: `DECISIONS.md` · `ROADMAP.md` · `KNOWN_ISSUES.md` · `CLAUDE_RULES.md`

## Estado general

| Capa | Estado | Notas |
|---|---|---|
| Domain | ✅ Completo | Entidades + base de auditoría |
| Infrastructure | ✅ Completo | EF Core, Identity, configuraciones |
| Application | ✅ Completo | Servicios, DTOs, Security Foundation |
| WinUI | ✅ Funcional | MVVM, SessionService, Observabilidad, UI módulos principales |

---

## Módulos implementados

| Módulo | Domain | Infra | Application | WinUI |
|---|---|---|---|---|
| POS / Ventas | ✅ | ✅ | ✅ | ✅ |
| Inventario (Entradas/Salidas/Traspasos/Ajustes) | ✅ | ✅ | ✅ | ✅ |
| Compras | ✅ | ✅ | ✅ | ✅ |
| Productos | ✅ | ✅ | ✅ | ✅ |
| Clientes / Proveedores | ✅ | ✅ | ✅ | ✅ |
| Caja | ✅ | ✅ | ✅ | ✅ |
| Configuración Global / Sucursal | ✅ | ✅ | ✅ | ✅ |
| Seguridad (Usuarios, Roles) | ✅ | ✅ | ✅ | ✅ |
| **Security Admin Module** (Perfiles, Permisos, Scopes, Arquitectura) | ✅ | ✅ | ✅ | ✅ |
| **Runtime Security Enforcement** (Productos, Entradas, Salidas, Existencias) | ✅ | ✅ | ✅ | ✅ |
| **Finanzas Operativas PYME** (Gastos, Ingresos, CxC, CxP) | ✅ | ✅ | ✅ | ✅ |
| **Sales Core** (Clientes+, Cotizaciones, Pedidos, OT ligeras) | ✅ | ✅ | ✅ | ✅ |

---

## Security Foundation (implementado 2026-05-07)

### Nuevas tablas en BD

| Tabla | Schema | Propósito |
|---|---|---|
| `Perfil` | seguridad | Perfiles de permisos reutilizables |
| `PerfilPermiso` | seguridad | Perfil ↔ Permiso N:N |
| `UsuarioPerfil` | seguridad | Usuario ↔ Perfil asignación directa |
| `UsuarioAlmacen` | seguridad | Scope de almacén por usuario |

### Nuevos servicios Application

| Servicio | Propósito |
|---|---|
| `IErpAuthorizationService` | Motor de autorización runtime (`PuedeAsync`) |
| `ISecurityContextService` | Snapshot completo de seguridad del usuario |
| `ISecurityScopeResolver` | Resolución de scopes empresa/sucursal/almacén |
| `IPerfilService` | Gestión CRUD de perfiles |
| `MemoryPermissionCache` | Caché en memoria de permisos efectivos (TTL 10min) |

### Cambios en servicios existentes

| Componente | Cambio |
|---|---|
| `ISessionContext` | Nueva propiedad `Guid? UsuarioId` |
| `SessionService` | Implementa `UsuarioId` desde `Usuario.Id` |
| `PermisoService` | Nivel 2 de evaluación: permisos de perfiles |
| `ServiceCollectionExtensions` | Registro de nuevos servicios; `MemoryPermissionCache` reemplaza `NullPermissionCache` |
| `RuntimeDiagnosticService` | Incluye `SecurityRuntimeSnapshot` opcional |
| `RuntimeContextSnapshot` | Campo `SecuritySnapshot` añadido |

### Datos seed

- **10 módulos**: ventas, inventario, caja, compras, productos, clientes, proveedores, configuracion, seguridad, reportes
- **51 permisos** con claves en formato `entidad.accion`
- **8 roles** nuevos (SuperAdmin, AdministradorEmpresa, GerenteSucursal, SupervisorInventario, OperadorInventario, Cajero, Vendedor, SupervisorVentas)
- **5 perfiles** iniciales con permisos asignados
- RolPermiso configurado para todos los roles

---

## Security Administration Module (implementado 2026-05-08)

### Ubicación en UI

`Configuración Global → Seguridad` — 6 tabs:

| Tab | Página | ViewModel | Propósito |
|---|---|---|---|
| Usuarios | `UsuariosPage` | `UsuariosViewModel` | Grid con Roles+Perfiles; Asignar Roles/Perfiles/Scopes |
| Roles | `RolesPage` | `RolesViewModel` | Grid con CantidadPermisos+CantidadUsuarios; Asignar Permisos |
| Perfiles | `PerfilesPage` | `PerfilesViewModel` | CRUD completo; Administrar Permisos por perfil |
| Permisos | `PermisosPage` | `PermisosViewModel` | Solo lectura; todos los permisos del sistema |
| Scopes | `ScopesPage` | `ScopesViewModel` | Asignación de sucursales+almacenes por usuario |
| Arquitectura Seguridad | `ArquitecturaSegPage` | *(static)* | Guía técnica viva integrada |

### Nuevos DTOs Application

| DTO | Propósito |
|---|---|
| `UsuarioResumenDto` | Usuario con RolesTexto + PerfilesTexto para grid admin |
| `RolAdminDto` | Rol con CantidadPermisos + CantidadUsuarios |
| `PermisoAdminDto` | Permiso con ModuloNombre para vista de solo lectura |
| `ScopeUsuarioDto` | Scopes del usuario: sucursales + almacenes como texto |
| `SucursalScopeItem` / `AlmacenScopeItem` | Items para selectores de scope |

### Nuevo servicio Application

**`ISecurityAdminService`** / **`SecurityAdminService`** (`Scoped`) — servicio dedicado a la UI de administración. NO reemplaza el motor de autorización runtime.

Operaciones disponibles:
- `ListarUsuariosConDetalleAsync` — usuarios con roles/perfiles precargados
- `ListarRolesConDetalleAsync` — roles con conteos de permisos y usuarios
- `ListarPermisosAsync` — todos los permisos del sistema (solo lectura)
- `ObtenerPermisosDePerfilAsync` — IDs de permisos de un perfil (para diálogos)
- `ObtenerPermisosDeRolAsync` / `AsignarPermisosARolAsync` — gestión de permisos por rol
- `ListarScopesUsuariosAsync` — resumen de scopes de todos los usuarios
- `ListarSucursalesDisponiblesAsync` / `ListarAlmacenesDisponiblesAsync`
- `ObtenerSucursalesDeUsuarioAsync` / `AsignarSucursalesAUsuarioAsync`
- `ObtenerAlmacenesDeUsuarioAsync` / `AsignarAlmacenesAUsuarioAsync`
- `ObtenerPerfilesDeUsuarioAsync` / `AsignarPerfilesAUsuarioAsync`

### Diálogos (ContentDialog inline, requieren XamlRoot)

| Diálogo | Acceso desde | Descripción |
|---|---|---|
| Nuevo/Editar Perfil | PerfilesPage | Form: Nombre, Descripción, Activo |
| Administrar Permisos (perfil) | PerfilesPage | Checklist agrupado por módulo |
| Asignar Permisos (rol) | RolesPage | Checklist agrupado por módulo |
| Asignar Roles (usuario) | UsuariosPage | Checklist de roles disponibles |
| Asignar Perfiles (usuario) | UsuariosPage | Checklist de perfiles activos |
| Asignar Scopes (usuario) | UsuariosPage + ScopesPage | Sucursales + Almacenes |

### Tab Arquitectura Seguridad

Página estática (`ArquitecturaSegPage`) integrada como documentación viva:
- Flujo de autorización runtime (diagrama ASCII)
- Ejemplos CORRECTO vs INCORRECTO de implementación
- Scopes de seguridad con ejemplos de código
- Developer Guidance Panel por módulo (Productos, Salida, Venta)
- Tabla de módulos y claves de permisos
- Tabla de roles predefinidos
- Modelo de evaluación de permisos (3 niveles)

---

## Arquitectura de capas

```
┌─────────────────────────────────────────────────┐
│  Ybridio.WinUI (presentación)                   │
│  - MVVM: CommunityToolkit.Mvvm 8.4.0            │
│  - SessionService → ISessionContext             │
│  - WorkspaceService (tabs persistentes)          │
│  - RuntimeDiagnosticService (observabilidad)     │
│  - IErpAuthorizationService (consumo en UI)      │
├─────────────────────────────────────────────────┤
│  Ybridio.Application (lógica de negocio)        │
│  - IErpAuthorizationService / ErpAuthorizationService │
│  - ISecurityContextService / SecurityContextService   │
│  - ISecurityScopeResolver / SecurityScopeResolver     │
│  - IPerfilService / PerfilService               │
│  - IPermisoService / PermisoService (3 niveles)  │
│  - MemoryPermissionCache                         │
│  - PermisosClave (constantes tipadas)            │
│  - ServiceResult<T>, ErrorCode                   │
├─────────────────────────────────────────────────┤
│  Ybridio.Infrastructure (persistencia)          │
│  - ErpDbContext (IdentityDbContext + 9 nuevos DbSets) │
│  - ApplicationUser / ApplicationRole            │
│  - EF Configurations (+ 4 nuevas de seguridad)  │
│  - ISessionContext / NullSessionContext         │
│  - Audit: ISchemaAuditService / IDatabaseAuditService │
├─────────────────────────────────────────────────┤
│  Ybridio.Domain (entidades)                     │
│  - Entidades base: AuditableEntity, CreationAuditEntity │
│  - Seguridad: Modulo, Permiso, RolPermiso, UsuarioPermiso │
│  - Seguridad (nuevas): Perfil, PerfilPermiso, UsuarioPerfil, UsuarioAlmacen │
│  - UsuarioSucursal (scope de sucursal)          │
└─────────────────────────────────────────────────┘
```

---

## DB Schema layout

```
core/        → Empresa, Sucursal, Cliente, Proveedor, Producto, ProductoCategoria, ProductoSucursal
catalogos/   → CategoriaProducto, TipoProducto, TipoImpuesto, UnidadMedida, ...
inventario/  → Almacen, Existencia, Entrada, Salida, Traspaso, AjusteInventario, Movimiento...
finanzas/    → Caja, AperturaCaja, MovimientoCaja, TipoMovimientoCaja
               CategoriaFinanciera, MovimientoFinanciero
               CuentaPorCobrar, CuentaPorPagar
ventas/      → Venta, VentaDetalle, Factura
compras/     → OrdenCompra, RecepcionCompra, ...
seguridad/   → Usuario, Rol, UsuarioRol, Modulo, Permiso, RolPermiso, UsuarioPermiso,
               UsuarioSucursal, Perfil, PerfilPermiso, UsuarioPerfil, UsuarioAlmacen
               + Identity auxiliares: UsuarioClaim, RolClaim, UsuarioLogin, UsuarioToken
```

---

## Convenciones obligatorias

Ver `CLAUDE.md` para la referencia completa.

### Naming crítico
- Claves de permisos: `entidad.accion` minúsculas (e.g., `venta.crear`, `salida.autorizar`)
- Servicios: `IXxxService` / `XxxService`; DTOs: `sealed record XxxDto`
- Lifetime: Servicios = Scoped; ViewModels = Transient; Caches = Singleton

### Reglas de seguridad
- **NUNCA**: `if (rol == "Admin")` → usar `await _auth.PuedeAsync(clave)`
- **SIEMPRE**: claves desde `PermisosClave.*` (nunca strings literales en código)
- **INVALIDAR CACHÉ** tras cambio de roles/permisos: `await _cache.InvalidateAsync(userId)`

---

## Próximos pasos (no implementados)

| Feature | Prioridad | Dependencias |
|---|---|---|
| UI de gestión de perfiles | Media | PerfilService listo |
| Enforcement visual (ocultar botones) | Alta | IErpAuthorizationService listo |
| Administración de UsuarioPermiso (overrides) | Media | IPermisoService listo |
| UsuarioAlmacen UI (asignación almacenes) | Baja | UsuarioAlmacen en BD |
| Redis para IPermissionCache | Baja | Interface lista |
| Auditoría cambios de seguridad | Media | — |
| MFA | Baja | — |
