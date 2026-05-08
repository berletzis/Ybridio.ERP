# Architecture Status — Ybridio ERP

> Última actualización: 2026-05-07

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
finanzas/    → Caja, AperturaCaja, MovimientoCaja, ...
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
