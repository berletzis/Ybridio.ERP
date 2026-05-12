# Architecture Status — Ybridio ERP

> Última actualización: 2026-05-12 → **ADR-041: Operational Editable Document Lines Pattern** (líneas con INPC, edición inline cantidad, recálculo importe en tiempo real, Single Source of Truth cálculo)  
> Build: ✅ 0 errores | BD: YBRIDIO-26 | Docs relacionados: `DECISIONS.md` · `ROADMAP.md` · `KNOWN_ISSUES.md` · `CLAUDE_RULES.md`  
> Fix crítico reciente: ADR-041 Editable Document Lines; ADR-040 Operational Commercial Document Standard; ADR-039 Shared Document Session; ADR-038 Directorio SoT + GetOrCreate; ADR-037 Selector Institucional

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
| **Document Workflow UX** (Workspace tabs, formularios, workflows) | ✅ | — | ✅ | ✅ |
| **Cotizaciones Operacionales** (Cliente real, Producto real, Existencia informativa) | ✅ | — | ✅ | ✅ |
| **Sales Transaction Layer** (Venta documental, Cobro, CxC crédito, Descuento inventario) | ✅ | ✅ | ✅ | ✅ |
| **Workflow Actions Layer** (Pedido→Venta, OT→Entregada, Navegación Cruzada Venta↔Pedido) | — | — | — | ✅ |
| **Inventory Operational Completion Layer** (Kardex operacional, stock bajo, trazabilidad, existencias seguras) | — | — | ✅ | ✅ |
| **Operational Inventory Experience** (Navegación VentaOrigen, columnas Usuario/Proveedor/VentaId, estado visual stock, dashboard light) | — | — | ✅ | ✅ |
| **Document Surface Visual Separation** (Pedidos/OT/Ventas Documentales → inline Document Surface, anti-pattern tabs eliminado) | — | — | — | ✅ |
| **Document Surface + Window Mode Institucional** (Cotizaciones piloto: inline contextual + ventana standalone; eliminado Detach/Split/Hybrid — ADR-032) | — | — | — | ✅ |
| **Visual Design System** (Styles/ source of truth; App.xaml bootstrap puro; Buttons/DataGrid/Forms/Tabs subdominios — ADR-033) | — | — | — | ✅ |
| **Operational Grid Standard v2** (Column Density System: Primary Expandable/Compact Semantic/Financial Compact; Financial Formatting Semantics: DecimalToCurrencyConverter, OgCurrencyTextStyle — ADR-035; piloto: Cotizaciones ✓ Clientes ✓) | — | — | — | ✅ |
| **RelacionComercial Entity Selector** (Control institucional reusable ADR-037/ADR-038; búsqueda incremental + debounce + cancellation; selector migrado a Directorio directo — Persona+EmpresaComercial; GetOrCreate pattern al guardar; migrado: Cotizaciones ✓ Pedidos ✓ Ventas ✓ OT ✓) | — | — | ✅ | ✅ |
| **Shared Document Session Pattern** (ADR-039: Detach rehostea la misma instancia de página/ViewModel; preserva runtime state completo; no auto-save; no recreación; implementado en Cotizaciones) | — | — | — | ✅ |

---

## Document Surface Visual Separation Standard — ADR-031 (implementado 2026-05-10)

### Problema resuelto

Tabs documentales ensimados/translúcidos en Pedidos, Órdenes de Trabajo y Ventas Documentales: los CRUDs simples se abrían como tabs de workspace, produciendo doble jerarquía visual y apariencia browser/IDE.

### Solución aplicada

| Módulo | Antes (anti-pattern) | Ahora (ADR-031) |
|---|---|---|
| Clientes | `_workspace.OpenTab(...)` | Inline Document Surface (ADR-030 Fase 1) |
| Productos | `_workspace.OpenTab(...)` | Inline Document Surface (ADR-030 Fase 2) |
| Cotizaciones | Tab workspace piloto | Inline + Detachable + Window Detach (ADR-025/027/028) |
| **Pedidos** | `_workspace.OpenTab(...)` | **Inline Document Surface (ADR-031)** |
| **Órdenes de Trabajo** | `_workspace.OpenTab(...)` | **Inline Document Surface (ADR-031)** |
| **Ventas Documentales** | `_workspace.OpenTab(...)` | **Inline Document Surface (ADR-031)** |

### Jerarquía visual oficial (obligatoria)

```
Tabs módulo (navegación principal)
    ↓
Document Surface Header
    breadcrumb: Módulo › Título | badge estado | botón ← volver
    ↓
Toolbar operacional (CommandBar del documento)
    ↓
Contenido formulario / grid de detalles
```

### Estado de build

✅ 0 errores de compilación después de la migración.

### Archivos cambiados

- `PedidosViewModel.cs`: estado de surface explícito (`IsDocumentSurfaceVisible`, `DocumentSurfaceContent`)
- `OrdenesTrabajoViewModel.cs`: estado de surface explícito
- `VentasDocumentalesViewModel.cs`: estado de surface explícito
- `PedidosPage.xaml` + `.cs`: listado ocultable + `ContentPresenter` inline; eliminado `_workspace.OpenTab`
- `OrdenesTrabajoPage.xaml` + `.cs`: mismo patrón
- `VentasDocumentalesPage.xaml` + `.cs`: mismo patrón
- `PedidoDocumentoPage.xaml` + `.cs`: header operacional + `OnCerrar` callback
- `OrdenTrabajoDocumentoPage.xaml` + `.cs`: header operacional + `OnCerrar` callback
- `VentaDocumentoPage.xaml` + `.cs`: header operacional + `OnCerrar` callback

### WorkspaceService — uso reservado

`IWorkspaceService.OpenTab` / `OpenOrActivateDocumentTabAsync` quedan reservados para:
- Navegación cruzada entre documentos relacionados (ej: abrir Pedido origen desde una Venta)
- Workflows multi-paso complejos (OT diseño → producción → QA)
- Análisis persistente multi-documento

**Documentación completa**: `Documentation/ADR-031-Document-Surface-Visual-Separation-Standard.md`

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
│    ✅ Single-instance policy (ADR-021)          │
│    ✅ OpenOrActivateDocumentTabAsync helper     │
│    ✅ Key/title conventions formalizadas        │
│  - WindowManager → IWindowManager (ADR-029)      │
│    ✅ Single source of truth window lifecycle   │
│    ✅ Detached windows policy (máx 2)           │
│    ✅ Convention key prefix enforcement         │
│    ✅ Ownership Win32 automático + cleanup      │
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

## Operational Inventory Experience (implementado 2026-05-09)

### Objetivo
Transformar los tabs de Inventario en experiencia operacional runtime conectada al flujo vivo del ERP sin complejidad enterprise:

- ✅ **Trazabilidad documental** — VentaId visible en Salidas, navegación directa a venta origen
- ✅ **Visibilidad de usuario** — UsuarioNombre en Entradas y Salidas para operación auditable
- ✅ **Visibilidad de proveedor** — ProveedorNombre en Entradas para contexto de compra/recepción
- ✅ **Estado visual stock** — Indicador ● (gris/amber/rojo) en Existencias según Cantidad/StockMinimo
- ✅ **Dashboard light** — Strip operacional en InventarioPage con contexto "Inventario Operacional"

### DTOs enriquecidos (Application)

| DTO | Campos agregados | Propósito |
|---|---|---|
| `SalidaResumenDto` | `long? VentaId`, `string? UsuarioNombre` | Salidas muestran venta origen + usuario aplicación |
| `EntradaResumenDto` | `string? ProveedorNombre`, `string? UsuarioNombre` | Entradas muestran proveedor + usuario aplicación |
| `ExistenciaDto` | `string EstadoStock` (computed) | Estado visual: "Normal" \| "Bajo" \| "Agotado" |

### Servicios actualizados (Application)

| Servicio | Cambio |
|---|---|
| `SalidaService.ListarAsync` | Proyecta `VentaId` (directo) y `UsuarioNombre` (batch lookup con `_context.Users.ToDictionaryAsync`) |
| `EntradaService.ListarAsync` | Include `Proveedor`, proyecta `ProveedorNombre` + `UsuarioNombre` (batch lookup) |

**Batch pattern anti-N+1**:
```csharp
var usuarioIds = lista.Where(...).Select(e => e.UsuarioAplicacionId!.Value).Distinct().ToList();
var userNames = await _context.Users.Where(u => usuarioIds.Contains(u.Id))
    .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "—", ct);
```

### Navegación operacional (WinUI)

| Origen | Destino | Implementación |
|---|---|---|
| `SalidasPage` → Venta Origen | `VentaDocumentoPage` | Evento `VentaOrigenSolicitada` en `SalidasViewModel`, handler en code-behind con `IWorkspaceService.OpenTab` |

**Pattern WorkspaceService**:
1. ViewModel expone evento (`EventHandler<long>? VentaOrigenSolicitada`)
2. Command `AbrirVentaOrigenCommand` dispara evento
3. Page code-behind suscribe evento en ctor, desuscribe en `OnNavigatedFrom`
4. Handler usa `IVentaDocumentalService.ObtenerConDetallesAsync` + `_workspace.OpenTab(key, title, icon, factory, closable)`

### UI actualizada (WinUI Views)

| Page | Cambios grid | Cambios toolbar |
|---|---|---|
| `SalidasPage.xaml` | Columnas `Venta #` y `Usuario` agregadas | Botón "Abrir Venta Origen" (Glyph `&#xE8A7;`) → Command |
| `EntradasPage.xaml` | Columnas `Proveedor` y `Usuario` agregadas | — (navegación proveedor omitida, módulo compras no implementado) |
| `ExistenciasPage.xaml` | Columna `●` (indicador estado) agregada; binding `EstadoStock` + `StockStateToColorConverter` | — |
| `InventarioPage.xaml` | Dashboard light strip (Grid Row 0): `"Inventario Operacional"` + placeholder indicadores futuros | — |

### Converter agregado (WinUI Converters)

**`StockStateToColorConverter`** — mapea string `"Normal"` \| `"Bajo"` \| `"Agotado"` → `Color` discreto:
- Normal → `#737373` (gris)
- Bajo → `#E08000` (amber)
- Agotado → `#C42B1C` (rojo)

Registrado en `ExistenciasPage.Resources` y usado con `x:Bind EstadoStock, Converter={StaticResource StockColorConverter}, Mode=OneWay`.

### Decisión: NO crear WMS enterprise
- **Mantener simplicidad PYME** — Inventario refleja operación viva sin costeo avanzado, supply chain o warehouse management complejo
- **Navegación selectiva** — Solo se implementa navegación a documentos ya existentes en UI (Venta); compras/OC pendientes
- **Dashboard light future** — Placeholder para indicadores futuros ("X bajo stock", "Y salidas hoy") sin dashboard BI pesado

### Beneficios operacionales

✅ Usuario ve **qué venta bajó stock** directamente desde Salidas  
✅ Usuario identifica **quién aplicó entrada/salida** en tiempo de auditoría operacional  
✅ Usuario detecta **stock bajo/agotado** visualmente sin reportes adicionales  
✅ Navegación **fluida entre tabs y documentos** vía WorkspaceService existente  
✅ Experiencia **operacional PYME** sin complejidad ni refactorings de arquitectura  

---

## Workspace Operational UX Stabilization (implementado 2026-05-09)

### Problema operacional

Caos en tabs/documentos del ERP:
- Tabs duplicados del mismo documento (e.g., Venta #91 abierta múltiples veces)
- Foco inconsistente (documentos abiertos en background sin activar)
- Código repetitivo en páginas (patrón `Exists() → ActivateTab()` vs `OpenTab()` manual)
- Keys/titles sin convención formal

### Solución implementada

**ADR-021**: Single-document-instance policy + helper centralizado `IWorkspaceService.OpenOrActivateDocumentTabAsync<TData>`

| Componente | Cambio |
|---|---|
| `IWorkspaceService` | Agregado contrato `Task<WorkspaceTabItem?> OpenOrActivateDocumentTabAsync<TData>(...)` |
| `WorkspaceService` | Implementado helper con lógica `Exists() → ActivateTab()` vs `await dataLoader() → OpenTab()` |
| `SalidasPage.xaml.cs` | Refactorizado `OnVentaOrigenSolicitada` (navegación a Venta origen) |
| `PedidosPage.xaml.cs` | Refactorizado `AbrirPedidoEnWorkspace` (apertura Pedido desde grid) |
| `OrdenesTrabajoPage.xaml.cs` | Refactorizado `AbrirOTEnWorkspace` (apertura OT desde grid) |
| `CotizacionesPage.xaml.cs` | Refactorizado `AbrirCotizacionEnWorkspace` (apertura Cotización desde grid) |
| `PedidoDocumentoPage.xaml.cs` | Refactorizado `AbrirOTEnWorkspace`, `AbrirVentaEnWorkspace` (workflows Pedido → OT/Venta) |
| `CotizacionDocumentoPage.xaml.cs` | Refactorizado `AbrirPedidoEnWorkspace` (workflow Cotización → Pedido) |
| `VentaDocumentoPage.xaml.cs` | Refactorizado `BtnAbrirPedidoOrigen_Click` (navegación a Pedido origen desde Venta) |

### Convenciones formalizadas

**Key formats** (determinan deduplicación):
- Documentos guardados: `{tipo}-{id}` (e.g., `venta-91`, `pedido-55`, `ot-12`)
- Documentos nuevos: `{tipo}-nueva-{guid}` (e.g., `venta-nueva-abc123`)
- Módulos operacionales: `{modulo}` (e.g., `inventario`, `dashboard`)

**Title formats** (runtime display):
- Documentos guardados: `{Tipo} #{id}` (e.g., `Venta #91`, `OT #12`)
- Documentos nuevos: `Nuevo/Nueva {Tipo}` (e.g., `Nueva Venta`)
- Módulos: nombre completo (e.g., `Inventario`)

### Comportamiento runtime

✅ **Single-instance**: un solo tab por documento operacional (Venta, Pedido, OT, Cliente, Producto)  
✅ **Tab reuse**: si el documento ya existe, activa el tab existente (no crea duplicado)  
✅ **Tab activation**: workflow abre/activa automáticamente el tab (no deja tabs invisible en background)  
✅ **Context preservation**: `WorkspaceTabItem.Content` mantiene `Page` viva → preserva filtros, selección, scroll, estado ViewModel  
✅ **Error handling**: callback `onError` opcional en helper para manejar fallos de carga  

### Anti-patterns evitados

❌ Tabs duplicados del mismo documento  
❌ Tabs abiertos sin foco (invisible)  
❌ Código repetitivo `Exists() / ActivateTab() / OpenTab()` en múltiples páginas  
❌ Keys/titles ambiguos o inconsistentes  
❌ Pérdida de contexto runtime al navegar  

### Documentación actualizada

- `docs/CLAUDE_RULES.md`: nueva sección 15 "Workspace Operational UX Stabilization" con reglas, convenciones, ejemplos, anti-patterns; nueva sección 16 "Workspace Visual Hierarchy" con capas visuales, estilos, spacing, hierarchy intent, anti-patterns
- `docs/DECISIONS.md`: ADR-021 "Workspace Single-Instance Policy & Centralized Helper"; ADR-022 "Workspace Visual Hierarchy" (Workspace Layer vs Module Layer)
- `docs/ARCHITECTURE_STATUS.md`: este documento (Workspace con ✅ single-instance policy + helper + convenciones + jerarquía visual de dos capas)

### Beneficios UX

✅ Workspace **estable, ordenado, predecible, ERP-like** (no browser tabs caótico)  
✅ Usuario navega workflows multi-documento (Cotización → Pedido → Venta) sin duplicados  
✅ Tab activation automática garantiza foco correcto post-workflow  
✅ Context preservation elimina reload innecesario al cambiar tabs  
✅ Código simplificado y consistente en páginas operacionales  
✅ **Jerarquía visual clara**: documentos persistentes (Workspace Layer) visualmente dominantes, navegación de módulo (Module Layer) visualmente secundaria  
✅ **Sin ensimamiento visual**: Workspace Tabs y Module Tabs diferenciados inmediatamente (spacing, background, height, padding)  
✅ **Experiencia operacional limpia**: operar múltiples documentos durante horas sin confusión visual  

---

## Workspace Visual Hierarchy (implementado 2026-05-09)

### Objetivo

Evitar caos visual donde Workspace Tabs (documentos persistentes) y Module Tabs (navegación interna) parezcan un solo control ensimado.  
Establecer jerarquía visual clara con dos estilos XAML diferenciados y spacing vertical explícito.

### Arquitectura de dos capas visuales

**Workspace Layer** (documentos persistentes):  
- Estilo: `WorkspaceTabItemStyle` (`App.xaml`)  
- Ubicación: `WorkspaceTabView` en `ShellPage.xaml` (Col 1, Capa 2, se superpone al ModuleFrame cuando visible)  
- Rol: documentos abiertos persistentes (Venta #91, Pedido #55, OT #12)  

**Module Layer** (navegación interna):  
- Estilo: `OutlookTabItemStyle` (`App.xaml`)  
- Ubicación: TabViews en páginas de módulos (`VentasPage.xaml`, `FinanzasPage.xaml`, `InventarioPage.xaml`, `ConfiguracionPage.xaml`)  
- Rol: navegación operacional del módulo activo (Cotizaciones, Pedidos, Ventas, Gastos, Ingresos)  

### Diferenciación visual

| Característica | Workspace Layer | Module Layer |
|---|---|---|
| **Estilo** | `WorkspaceTabItemStyle` | `OutlookTabItemStyle` |
| **MinHeight** | 48px | 40px |
| **Padding** | 18,12,6,12 | 16,8,4,8 |
| **Background (normal)** | SubtleFillColorSecondaryBrush | Transparent |
| **Background (selected)** | LayerFillColorDefaultBrush | Transparent |
| **SelectionBar** | Height=4, Margin=8,0 | Height=3, Margin=6,0 |
| **CloseButton** | 22x22 | 20x20 |
| **Separación vertical** | Margin 0,12,0,0 | — |
| **Jerarquía visual** | Dominante, principal | Secundario, contextual |

### Implementación

**App.xaml**:  
- `WorkspaceTabItemStyle`: dominante, persistente, documentos abiertos  
- `OutlookTabItemStyle`: compacto, navegacional, tabs de módulo  

**ShellPage.xaml**:  
```xaml
<TabView x:Name="WorkspaceTabView"
         Margin="0,12,0,0"
         Background="{ThemeResource LayerOnMicaBaseAltFillColorDefaultBrush}">
    <TabView.Resources>
        <Style TargetType="TabViewItem" BasedOn="{StaticResource WorkspaceTabItemStyle}"/>
    </TabView.Resources>
</TabView>
```

**Module Pages** (VentasPage, FinanzasPage, InventarioPage, ConfiguracionPage):  
```xaml
<Page.Resources>
    <Style TargetType="TabViewItem" BasedOn="{StaticResource OutlookTabItemStyle}"/>
</Page.Resources>

<!-- Visual Container Hierarchy: Border wrapper obligatorio -->
<Border Padding="16,12,16,16"
        Background="{ThemeResource LayerFillColorDefaultBrush}">
    <TabView x:Name="ModuleTabs"
             Background="Transparent"
             TabWidthMode="SizeToContent"
             IsAddTabButtonVisible="False">
        <!-- TabViewItems -->
    </TabView>
</Border>
```

### Visual Container Hierarchy (implementado 2026-05-09)

**Problema identificado**: A pesar de ADR-022 (estilos diferenciados + margin 12px), persistía efecto "tabs transparentes/ensimados" porque los Module TabViews no tenían **container boundary físico**. Los TabViews estaban directamente en la Page raíz sin separación visual real del Workspace Layer.

**Solución**: Border wrapper obligatorio con:
1. **Padding superior 12px** — separación física desde el borde de página
2. **Background sólido sutil** — `LayerFillColorDefaultBrush` (Outlook 2026 compliant)
3. **TabView Background="Transparent"** — el fondo lo provee el Border container

**Implementación**:
- VentasPage.xaml: TabView envuelto en Border con Padding="16,12,16,16"
- FinanzasPage.xaml: mismo patrón
- InventarioPage.xaml: Border en Grid.Row="1" (dashboard strip en Row 0 preservado)
- ConfiguracionPage.xaml: Border wrapper para TabsGlobal y TabsTienda (nested TabsSeguridad Background="Transparent")

**Patrón container estándar**:
```xaml
<Border Padding="16,12,16,16"
        Background="{ThemeResource LayerFillColorDefaultBrush}">
    <TabView Background="Transparent" ...>
        <!-- Module navigation tabs -->
    </TabView>
</Border>
```

**Resultado**:
- Module Layer ahora se siente **contenido dentro del documento activo**
- Workspace Layer se siente **externo/superior**
- **Separación física visible** — NO tabs flotando sobre tabs
- **Boundary claro** — container background sólido vs Workspace background
- **Eliminación completa** del efecto "tabs ensimados/transparentes"

---

### Workspace TabView Content Host Separation (implementado 2026-05-09)

**Problema identificado**: A pesar de ADR-022 (estilos diferenciados) y ADR-023 (Border wrapper Module Layer), persistía un **overlap visual interno en el WorkspaceTabView** donde el header region (TabViewItem 48px + SelectionBar 4px) invadía visualmente el content host, causando:
- **Underline azul cae encima del contenido documental**
- **Contenido inicia demasiado arriba** (pegado al header)
- **Tabs workspace parecen ensimados sobre el contenido**
- **Margin top (12px) insuficiente** — solo separa WorkspaceTabView del ModuleFrame, NO separa el content host interno del header region

**Causa técnica**: WinUI TabView coloca el TabViewItem header y el content host **inmediatamente adyacentes sin separación vertical estructural interna**. El SelectionBar (4px height) queda visualmente superpuesto al contenido.

**Solución**: Padding top estructural de **60px** directamente en el `WorkspaceTabView` (ShellPage.xaml) para crear separación física real entre header region y content host.

**Cálculo de Padding**:
```
Padding top = TabItem.MinHeight + SelectionBar.Height + Visual Spacing
            = 48px + 4px + 8px
            = 60px
```

**Implementación ShellPage.xaml**:
```xaml
<TabView x:Name="WorkspaceTabView"
         IsAddTabButtonVisible="False"
         TabWidthMode="SizeToContent"
         Visibility="Collapsed"
         Margin="0,12,0,0"
         Padding="0,60,0,0"
         Background="{ThemeResource LayerOnMicaBaseAltFillColorDefaultBrush}">
    <TabView.Resources>
        <Style TargetType="TabViewItem" BasedOn="{StaticResource WorkspaceTabItemStyle}"/>
    </TabView.Resources>
</TabView>
```

**Diferencia con Module Layer (ADR-023)**:
- **Module TabViews**: Border wrapper **externo** con Padding (separación container del Workspace Layer superior)
- **WorkspaceTabView**: Padding **interno directo** (separación header region del content host dentro del mismo TabView)
- Ambas son soluciones estructurales complementarias: una separa containers entre layers, otra separa header/content dentro del workspace host

**Resultado**:
✅ **Header region separado físicamente del content host** — underline azul NO invade contenido  
✅ **Contenido documental con espacio respiratorio superior** — NO pegado al header  
✅ **Module Tabs completamente debajo del Workspace Layer** — jerarquía visual clara  
✅ **Sin overlap visual ni tabs ensimados** — separación estructural real  
✅ **UX limpia, profesional, operacional** — ERP estable, Outlook 2026 style  

**Anti-patterns evitados**:
❌ WorkspaceTabView sin Padding top (overlap inevitable)  
❌ Usar solo Margin para separación interna content host (NO afecta content interno)  
❌ Margins arbitrarios gigantes en documentos (hack frágil, NO escalable)  
❌ TranslateTransform/RenderTransform offsets (hack visual, NO estructural)  
❌ Z-index tricks (NO resuelve overlap real)  

**Documentación actualizada**:
- `Documentation/DECISIONS.md`: ADR-024 Workspace TabView Content Host Separation
- `Documentation/CLAUDE_RULES.md` §16: actualizada con Padding interno obligatorio
- `Documentation/ARCHITECTURE_STATUS.md`: este documento (estado Workspace runtime + visual)

---

## Security Runtime Concurrency Stabilization (implementado 2026-05-09)

### Problema identificado

**DbContext concurrency exceptions** durante navegación rápida runtime:
- Navegación rápida entre módulos (Clientes ↔ Cotizaciones ↔ Pedidos)
- Activación/desactivación Document Surfaces embebidos
- Múltiples tabs Workspace con pre-checks autorización concurrentes
- `OnNavigatedTo` + bindings runtime + `AsyncRelayCommand` pre-checks simultáneos
- Runtime Diagnostic Panel refresh + navegación usuario
- Resultado: `System.InvalidOperationException: "A second operation was started on this context instance before a previous operation completed."`

**Causa raíz**: Múltiples evaluaciones concurrentes de `PermisoService.TienePermisoAsync(...)` y `ObtenerPermisosEfectivosAsync(...)` usando el mismo `ErpDbContext` scoped durante navegación runtime.

### Solución implementada

**ADR-026**: Single-flight pattern en `PermisoService` usando `SemaphoreSlim` global para serializar evaluaciones de permisos runtime.

| Componente | Cambio |
|---|---|
| `PermisoService.TienePermisoAsync` | Agregado `await _authSemaphore.WaitAsync(ct)` antes de queries EF Core; `finally { _authSemaphore.Release(); }` |
| `PermisoService.ObtenerPermisosEfectivosAsync` | Agregado single-flight guard con double-check pattern (caché antes y después del lock) |
| `PermisoService` (campo) | `private static readonly SemaphoreSlim _authSemaphore = new(1, 1);` |

### Patrón aplicado

```csharp
// Single-flight guard: serializar evaluaciones de permisos runtime
await _authSemaphore.WaitAsync(ct);
try
{
    // Lógica de evaluación: override → perfiles → roles
    // (queries EF Core usando _context scoped)
}
finally
{
    _authSemaphore.Release();  // Siempre liberar en finally
}
```

**Por qué necesario**:
- `ErpDbContext` es **scoped** (correctamente según arquitectura EF Core)
- `PermisoService` es **scoped** (inyectado en `ErpAuthorizationService` scoped)
- Navegación runtime dispara **múltiples evaluaciones concurrentes** del mismo servicio scoped
- EF Core **NO permite operaciones concurrentes** en el mismo contexto scoped
- Serialización garantiza **un solo thread evaluando permisos a la vez** sin cambiar arquitectura DI

**Alternativas descartadas**:
- ❌ Per-permission semaphore con `ConcurrentDictionary`: overhead de tracking, limpieza de diccionario creciente
- ❌ Lock statement tradicional: semánticamente equivalente, semaphore más explícito para async/await
- ❌ Task.Run para aislar DbContext: anti-pattern prohibido (ADR-020), race conditions
- ❌ DbContext singleton: viola arquitectura EF Core, causaría más problemas de state
- ❌ Caché agresivo: `MemoryPermissionCache` ya existe, NO elimina problema en `TienePermisoAsync` individual
- ❌ Rehacer Security Foundation: NO permitido (regla crítica §3), problema es runtime concurrency

### Resultado runtime esperado

✅ **SIN exceptions DbContext** durante navegación rápida multi-módulo  
✅ **Autorización consistente** (permisos correctos aplicados)  
✅ **Navegación fluida** sin degradación UX perceptible  
✅ **Document Surfaces estables** (activar/desactivar sin crashes)  
✅ **Workspace tabs concurrentes** (pre-checks múltiples seguros)  
✅ **Runtime Diagnostic Panel compatible** (refresh automático sin colisiones)  

### Validación runtime requerida

El usuario debe validar:
1. Navegar rápidamente Clientes → Cotizaciones → Pedidos (cambiar tabs cada 1-2 segundos)
2. Abrir/cerrar Document Surfaces (Nueva Cotización → guardar → editar → volver) repetidamente
3. Ejecutar múltiples refresh simultáneos (F5 en varios módulos)
4. Confirmar ausencia de `InvalidOperationException` relacionada con DbContext
5. Verificar autorización correcta (comandos habilitados/deshabilitados según permisos)
6. Validar navegación fluida sin latencia excesiva

### Anti-patterns documentados

```csharp
// ❌ NO aislar DbContext con Task.Run
Task.Run(() => await _permisos.TienePermisoAsync(...));

// ❌ NO cambiar DbContext a singleton
services.AddSingleton<ErpDbContext>();  // PROHIBIDO

// ❌ NO capturar DbContext en campos static
private static ErpDbContext _ctx;  // PROHIBIDO

// ❌ NO usar lock tradicional sin considerar async
lock (_lock) { await _context.SaveChangesAsync(); }  // Deadlock risk
```

### Documentación actualizada

- `Documentation/DECISIONS.md`: ADR-026 Security Runtime Concurrency Stabilization
- `Documentation/CLAUDE_RULES.md` §7: nueva subsección "Security Runtime Concurrency"
- `Documentation/ARCHITECTURE_STATUS.md`: este documento (estado runtime security stabilization)
- `Documentation/KNOWN_ISSUES.md`: documentación problema DbContext concurrency original + solución

---
**Validación requerida**: Abrir múltiples documentos (Cotización, Pedido, Venta, OT) y verificar: 1) underline NO invade contenido, 2) tabs NO parecen ensimados, 3) separación clara Workspace Header/contenido, 4) UX limpia sin overlap, 5) responsive correcto múltiples DPI.

---

## Workspace Operational UX Stabilization (implementado 2026-05-09)

### Document Surface UX Standardization (implementado 2026-05-09)

**Objetivo**: Reducir el caos de Workspace Tabs innecesarios para operaciones CRUD ligeras, usando **Document Surfaces** embebidos dentro del módulo activo que reemplazan temporalmente el grid de listado.

**Problema identificado**:
- Operaciones CRUD simples (Nueva Cotización, Editar Cliente) generaban tabs persistentes en Workspace Layer
- Acumulación excesiva de tabs para tareas que normalmente se completan en una sola sesión
- Pérdida de contexto de módulo (usuario perdía visibilidad del listado al abrir tab workspace)
- UX fragmentada (navegación módulo ↔ workspace creaba fricción operacional)
- Flujo PYME ineficiente (`crear → guardar → seguir trabajando` requería cerrar tab manualmente)

**Solución implementada**: Document Surface UX Pattern (§ADR-025)

**Principio arquitectónico**:
```
Workspace Tabs      = workflows persistentes, multi-documento, complejos, importantes
Document Surfaces   = operación rápida contextual (Nuevo/Editar/Abrir) sin tab persistente
```

**Piloto implementado**:
- ✅ **Cotizaciones** (CotizacionesPage → CotizacionDocumentoPage)
- 🔲 Clientes (pendiente)
- 🔲 Productos (pendiente)

**NO migrados todavía** (workflows complejos, permanecen en Workspace Tabs):
- Pedidos (workflow complejo)
- Ventas (genera otros documentos)
- OT (multi-paso: diseño → producción → QA)

**Componentes nuevos**:

| Archivo | Propósito |
|---|---|
| `InverseBoolToVisibilityConverter.cs` | Converter para ocultar grid cuando surface está activo |
| `CotizacionesViewModel.IsDocumentSurfaceVisible` | Estado de visibilidad del surface |
| `CotizacionesViewModel.DocumentSurfaceContent` | Contenido del surface (Page embebida) |
| `CotizacionesViewModel.CerrarDocumentSurfaceAsync()` | Cierra surface y refresca grid |
| `CotizacionDocumentoViewModel.DocumentSaved` | Callback notifica guardado exitoso |
| `CotizacionDocumentoPage.VolverALista` | Callback cierra surface sin guardar |
| `CotizacionesPage.xaml` Grid overlay | ContentPresenter para surface + grid oculto condicionalmente |

**Reglas UX oficiales**:

1. **Layout: Content Replacement**
   - ContentPresenter reemplazable dentro del módulo
   - Un solo contenido visible: grid XOR Document Surface
   - NO split view permanente, NO grid de dos columnas

2. **Transiciones**
   - Transición instantánea o muy sutil
   - Sin animaciones complejas (ERP operacional debe sentirse rápido)

3. **Comportamiento Guardar**
   - Después de guardar: refrescar grid, cerrar surface, volver al listado
   - Flujo PYME típico: `crear → guardar → seguir trabajando en lista`

4. **Navegación "← Volver a Lista"**
   - Botón claro en CommandBar del Document Surface
   - Texto: "Volver a Lista" | Icon: `&#xE72B;` (Back)
   - Acción: cerrar surface sin guardar

**Arquitectura del pattern**:

```csharp
// ViewModel del módulo (listado)
[ObservableProperty] private bool isDocumentSurfaceVisible;
[ObservableProperty] private object? documentSurfaceContent;

public async Task CerrarDocumentSurfaceAsync()
{
    IsDocumentSurfaceVisible = false;
    DocumentSurfaceContent = null;
    await RefrescarAsync(); // Refrescar grid automáticamente
}

// ViewModel del documento
public Action? DocumentSaved;

[RelayCommand]
public async Task GuardarAsync()
{
    // ... lógica de guardado ...
    DocumentSaved?.Invoke(); // Notificar al módulo padre
}
```

```xaml
<!-- Page del módulo (XAML) -->
<Grid Grid.Row="2">
    <!-- Listado (visible cuando surface NO activo) -->
    <Border Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay, 
                                  Converter={StaticResource InverseBoolToVisibilityConverter}}">
        <ListView ItemsSource="{x:Bind ViewModel.Items, Mode=OneWay}" ... />
    </Border>

    <!-- Document Surface (visible cuando surface activo) -->
    <ContentPresenter Content="{x:Bind ViewModel.DocumentSurfaceContent, Mode=OneWay}"
                      Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay}"/>
</Grid>
```

**Resultado UX esperado**:

✅ **Menos caos de Workspace Tabs** — CRUDs ligeros NO generan tabs innecesarios  
✅ **Navegación más natural** — usuario permanece en contexto de módulo  
✅ **Contexto preservado** — grid oculto temporalmente, no perdido  
✅ **Operación más rápida** — sin navegación Workspace ↔ Módulo  
✅ **Flujo PYME cumplido** — `crear → guardar → cerrar automático → seguir trabajando`  
✅ **Runtime Observability funcional** — reportes de contexto correctos  
✅ **WorkspaceService intacto** — workflows complejos siguen usando tabs persistentes  

**Anti-patterns evitados**:

❌ Usar Document Surface para workflows complejos/multi-documento  
❌ Dejar surface abierto después de guardar (para CRUDs ligeros)  
❌ Implementar animaciones complejas de transición  
❌ Usar split view o layouts master-detail permanentes  
❌ Abrir Workspace Tabs para operaciones CRUD simples  
❌ Migrar todos los módulos de golpe sin validar piloto  
❌ Modificar/rehacer WorkspaceService, Shell, Runtime Observability  

**Próximos pasos piloto**:
- Replicar pattern a Clientes (ClientesPage → ClienteDocumentoPage)
- Replicar pattern a Productos (ProductosPage → ProductoDocumentoPage)
- Validar aceptación operacional con usuarios finales
- Confirmar estabilidad runtime y observabilidad correcta
- Expandir gradualmente a otros módulos CRUD ligeros según validación

**Referencia**: ADR-025, CLAUDE_RULES.md §12

---

### Document Surface Detachable Mode Extension (implementado 2026-05-09)

**Objetivo**: Extender el Document Surface UX Pattern con un **modo desacoplado opcional** que permite visualización simultánea grid+surface para escenarios de multitarea ligera controlada sin regresar a Workspace Tabs infinitos.

**Problema identificado**:
- Document Surface content replacement funciona correctamente para operación diaria PYME simple/rápida
- **PERO** existen escenarios operacionales válidos: comparar información, copiar datos, consultar lista mientras edita
- Limitación content replacement puro: usuario pierde visibilidad del grid cuando abre documento
- **NO queremos** volver a Workspace Tabs globales para CRUDs simples (caos UX evitado con ADR-025)
- Necesidad: multitarea ligera **ocasional bajo demanda usuario**, manteniendo simplicidad por defecto

**Solución implementada**: Document Surface Detachable Mode (§ADR-027)

**Principio arquitectónico extendido**:
```
Workspace Tabs             = workflows persistentes, multi-documento complejo, importante
Document Surfaces Normal   = operación rápida contextual (grid XOR surface) DEFAULT
Document Surface Detached  = multitarea ligera (grid + surface simultáneos) OPCIONAL
```

**Piloto implementado**:
- ✅ **Cotizaciones** (detachable mode completo)
- 🔲 Clientes (pendiente validación piloto)
- 🔲 Productos (pendiente validación piloto)

**Limitaciones arquitectónicas obligatorias**:
- SOLO 1 Document Surface desacoplada activa por módulo (NO múltiples surfaces simultáneas)
- Activación explícita mediante botón discreto "Desacoplar Surface" en CommandBar secundario documento
- Default SIEMPRE es content replacement mode (grid XOR surface)
- NO floating windows OS reales
- NO dock managers enterprise
- State preservation: filtros, selección, scroll mantienen durante acoplar/desacoplar

**Componentes nuevos ADR-027**:

| Archivo | Propósito |
|---|---|
| `BoolToVisibilityConverter.cs` | Converter directo bool→Visibility para mostrar split view cuando detached=true |
| `CotizacionesViewModel.IsDocumentSurfaceDetached` | Estado de modo desacoplado (false por defecto) |
| `CotizacionesViewModel.ToggleDetach()` | Alterna entre content replacement y split view |
| `CotizacionDocumentoPage.ToggleDetach` | Callback desacoplar invocado desde botón documento |
| `CotizacionesPage.xaml` dual layout | Rama normal (grid XOR surface) + rama detached (split columns 2*/3*) |

**Arquitectura detachable mode**:

```csharp
// ViewModel módulo (piloto Cotizaciones)
[ObservableProperty] private bool isDocumentSurfaceDetached;

[RelayCommand]
public void ToggleDetach()
{
    if (!IsDocumentSurfaceVisible) return; // Guard obligatorio
    IsDocumentSurfaceDetached = !IsDocumentSurfaceDetached;
}

public void AbrirNuevaCotizacion()
{
    DocumentSurfaceContent = null;
    IsDocumentSurfaceVisible = true;
    IsDocumentSurfaceDetached = false; // Default: content replacement
}

public async Task CerrarDocumentSurfaceAsync()
{
    IsDocumentSurfaceVisible = false;
    IsDocumentSurfaceDetached = false; // Reset detached state
    DocumentSurfaceContent = null;
    await RefrescarAsync();
}
```

```xaml
<!-- Page del módulo (XAML dual layout) -->
<Grid Grid.Row="2">
    <!-- Rama 1: Modo Normal / Content Replacement (default) -->
    <Grid Visibility="{x:Bind ViewModel.IsDocumentSurfaceDetached, Mode=OneWay, 
                               Converter={StaticResource InverseBoolToVisibilityConverter}}">
        <Border Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, ..., InverseBool...}">
            <ListView ... />
        </Border>
        <ContentPresenter Content="{x:Bind ViewModel.DocumentSurfaceContent, ...}"
                          Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, ...}"/>
    </Grid>

    <!-- Rama 2: Modo Desacoplado (split view side-by-side) -->
    <Grid Visibility="{x:Bind ViewModel.IsDocumentSurfaceDetached, Mode=OneWay, 
                               Converter={StaticResource BoolToVisibilityConverter}}">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="2*" MinWidth="400"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="3*" MinWidth="600"/>
        </Grid.ColumnDefinitions>
        <Border Grid.Column="0"><ListView ... /></Border>
        <Border Grid.Column="1" Width="1" Background="#E5E5E5"/>  <!-- Separador -->
        <ContentPresenter Grid.Column="2" Content="{x:Bind ViewModel.DocumentSurfaceContent, ...}"/>
    </Grid>
</Grid>
```

```xaml
<!-- Document Surface (XAML) -->
<CommandBar>
    <!-- Comandos primarios: Volver a Lista, Guardar, etc. -->
    ...
    <!-- Detachable Mode: acción avanzada opcional -->
    <CommandBar.SecondaryCommands>
        <AppBarButton Label="Desacoplar Surface" Click="BtnToggleDetach_Click">
            <AppBarButton.Icon><FontIcon Glyph="&#xE89A;"/></AppBarButton.Icon>
        </AppBarButton>
    </CommandBar.SecondaryCommands>
</CommandBar>
```

**UX Rules Detachable Mode**:

1. **Modo normal por defecto**: content replacement (grid XOR surface) es comportamiento DEFAULT
2. **Activación bajo demanda**: usuario activa detach mediante botón discreto CommandBar secundario
3. **Multitarea ligera controlada**: SOLO 1 surface desacoplada por módulo
4. **Split view limpio**: layout grid columnas con separador visual, NO floating windows, mantiene Outlook 2026 style
5. **State preservation**: filtros, selección, scroll preservados durante acoplar/desacoplar
6. **Reset automático**: estado detached resetea al cerrar surface (NO persistir entre aperturas)

**Resultado UX esperado ADR-027**:

✅ **UX simple por defecto** — content replacement sigue siendo modo normal PYME-friendly  
✅ **Multitarea ligera opcional** — usuario puede desacoplar cuando necesita comparar/consultar  
✅ **Sin caos Workspace Tabs** — CRUDs simples NO vuelven a generar tabs infinitos  
✅ **Split view limpio** — grid+surface simultáneos sin dock managers enterprise pesados  
✅ **State preservation** — filtros/selección mantienen al alternar modos  
✅ **Piloto acotado** — validación Cotizaciones antes de expandir a otros módulos  
✅ **Runtime Observability compatible** — NO genera overhead adicional ni regresiones DbContext  

**Anti-patterns evitados ADR-027**:

❌ Múltiples surfaces desacopladas simultáneas (límite 1 por módulo)  
❌ Floating windows OS reales (mantener embedded layout)  
❌ Dock managers enterprise complejos (overhead innecesario)  
❌ Split view permanente por defecto (UX ruido visual constante)  
❌ Detach sin Document Surface activo (guard obligatorio `if (!IsDocumentSurfaceVisible) return;`)  
❌ Persistir estado detached entre aperturas (resetear siempre en `AbrirNueva/Editar/Cerrar`)  
❌ Volver a Workspace Tabs para CRUDs simples (pattern evitado con ADR-025)  

**Validación runtime requerida**:
1. Nueva Cotización → desacoplar surface → navegar grid mientras edita → acoplar → guardar
2. Editar Cotización → desacoplar → seleccionar otra en grid → comparar → acoplar → continuar
3. Abrir/cerrar Document Surface repetidamente → confirmar reset estado detached correcto
4. Navegar entre módulos con surface desacoplada → confirmar lifecycle correcto sin leaks
5. Validar ausencia overlap visual, layout responsive, split view limpio
6. Confirmar NO regresión DbContext concurrency (compatible ADR-026 single-flight guard)

**Referencia**: ADR-027, CLAUDE_RULES.md §12.1 Detachable Extension

---

### Window Management Standards — Centralized Runtime Authority (implementado 2026-05-10)

**Objetivo**: Formalizar **`WindowManager` como single source of truth OBLIGATORIO** para TODO window lifecycle management en el ERP. Consolidar window runtime behavior bajo una autoridad centralizada, eliminar implementaciones paralelas, establecer policies globales enforcement, y definir anti-patterns oficiales explícitos.

**Problema identificado**:
- Window Detach Mode piloto inicial (ADR-028) creó servicio paralelo `IDetachedWindowManager`/`DetachedWindowManager`
- **Tracking duplicado** de ventanas activas (`_windows` en WindowManager + `_activeWindows` en DetachedWindowManager)
- **Lifecycle management inconsistente**: ownership Win32, cleanup handlers, z-order, focus dispersos entre managers
- **Policy enforcement fragmentado**: límite máximo 2 detached windows vivía en manager secundario, NO centralizado
- **Riesgo de leaks** por cleanup disperso y handlers duplicados en múltiples archivos
- **Violación DRY**: lógica `new Window()`, `AppWindow.GetFromWindowId(...)`, resize, activate, closed handlers duplicada
- **Manual window creation** disperso en Pages/ViewModels fuera de managers oficiales

**Solución implementada**: Window Management Standards (ADR-029)

**Principio arquitectónico oficial**:
```
WindowManager = ÚNICA autoridad runtime para window lifecycle
TODO window creation/tracking/policy/cleanup → centralizado bajo WindowManager
Convention key prefix → policy enforcement automático (ej: "detached:" → max 2)
Pages/ViewModels → SOLO una línea lógica: _windowManager.OpenWindow(...)
```

**Consolidación técnica ejecutada**:

| Acción | Detalle |
|---|---|
| ✅ Extensión `WindowManager.cs` | Contador `_detachedWindowsCount`, constantes `MaxDetachedWindows=2` y `DetachedKeyPrefix="detached:"`, validación límite en `OpenWindow` |
| ✅ Exception tipada | `DetachedWindowLimitException` operacional para UI (try/catch + ContentDialog) |
| ✅ Window helper code-only | `DetachedDocumentWindow.xaml.cs` en `Views/Detached/` (sin XAML, evita source-gen issues) |
| ✅ Migración UI | `CotizacionDocumentoPage.xaml.cs` → usa `IWindowManager`, key `"detached:cotizacion:{id}"`, try/catch exception |
| ✅ Eliminación duplicación | `IDetachedWindowManager.cs`, `DetachedWindowManager.cs`, DI registration secundaria eliminados |
| ✅ Build | 0 errores |

**Lifecycle centralizado `WindowManager`**:
- ✅ Creación vía factory pattern (`OpenWindow<TWindow, TKey>(key, factory, options)`)
- ✅ Ownership Win32 automático (z-order garantizado sobre MainWindow)
- ✅ Tamaño y posicionamiento (CenterOwner, CenterScreen, Cascade)
- ✅ Activación y focus multi-layer (`BringDescriptorToFront`)
- ✅ Reutilización instancias existentes vía key (NO duplicar ventanas)
- ✅ Tracking centralizado único (`_windows` dictionary)
- ✅ Cleanup automático (`window.Closed` handler registrado internamente)
- ✅ Policy enforcement global (ej: detached max 2, validado antes de crear)
- ✅ Logging diagnóstico centralizado `[WindowManager]`

**Detached Windows Policy oficial (ADR-028+029)**:
- Límite máximo global: **2 detached windows activas simultáneas**
- Convention enforcement: keys con prefix `"detached:"` activan policy automáticamente
- Exception operacional: `DetachedWindowLimitException` cuando límite alcanzado
- UI handling: try/catch + `ContentDialog` claro al usuario

```csharp
// Pattern oficial: detached window opening
var detachedKey = $"detached:cotizacion:{cotizacionId}";

try
{
    _windowManager.OpenWindow<DetachedDocumentWindow, string>(
        key: detachedKey,
        factory: () => new DetachedDocumentWindow(documentPage, titulo),
        options: new WindowOptions { Width = 1200, Height = 800 });
}
catch (DetachedWindowLimitException ex)
{
    // Mostrar ContentDialog operacional
    await MostrarMensajeLimiteVentanasAsync(ex);
}
```

**Anti-patterns oficiales PROHIBIDOS (ADR-029)**:

❌ `new Window()` fuera de factories pasadas a `WindowManager.OpenWindow(...)`  
❌ `AppWindow` manual disperso en Pages/ViewModels  
❌ Lifecycle handlers duplicados (`window.Closed += ...` fuera del manager)  
❌ Tracking paralelo ventanas (`_ventanasAbiertasPorMi` counters locales)  
❌ Services window management secundarios (`IDetachedWindowManager`, `IDialogManager`, etc.)  
❌ Window ownership Win32 manual fuera del manager  
❌ Policy enforcement fragmentado (validaciones límite en ViewModels/Pages)  

**Convention key prefixes oficiales**:
- `detached:` → máximo 2 simultáneas (ADR-028+029)
- `dialog:` → (futuro) máximo 3 simultáneas
- `wizard:` → (futuro) solo 1 activo
- `detail:` → sin límite

**Extensión futura**: Nuevas policies (ej: máx 3 dialogs, solo 1 wizard) se agregan **EXCLUSIVAMENTE** en `WindowManager.cs`. Convention key prefix permite políticas sin romper API.

**Runtime validation pendiente**:
1. Abrir ventana detached Cotización → confirmar creación correcta
2. Abrir segunda ventana detached → confirmar límite máximo 2 enforced
3. Intentar abrir tercera → confirmar `DetachedWindowLimitException` + ContentDialog
4. Cerrar ventana detached → confirmar cleanup + decremento contador
5. Reabrir ventana ya existente → confirmar reutilización vía key (NO duplicar)
6. Navegación rápida + detached windows → confirmar SIN leaks evidentes
7. Activación/focus ventanas múltiples → confirmar z-order correcto
8. Integración Runtime Diagnostic Panel → confirmar tracking visible (futuro)

**Estado**: ✅ **IMPLEMENTADO — Centralizado bajo WindowManager** (ADR-029). Build exitoso, tracking centralizado, policy global, anti-patterns formalizados.

**Documentación completa**: Ver `Documentation/ADR-029-Window-Management-Standards.md`, `Documentation/CLAUDE_RULES.md` §13

**Referencia**: ADR-029, KI-015, CLAUDE_RULES.md §3 (WindowManager agregado a lista NO Rehacer)

---

### Anti-patterns evitados

❌ Usar el mismo estilo para ambas capas (confusión visual)  
❌ Tabs workspace sin separación vertical del módulo (ensimamiento)  
❌ Module tabs con height/padding igual a workspace (jerarquía rota)  
❌ Backgrounds agresivos o colores llamativos (mantener Outlook 2026 sutil)  
❌ TabView dentro de TabView sin diferenciación clara  
❌ **Module TabView sin container boundary físico** (tabs transparentes/ensimados)  
❌ **Module TabView con Background="Transparent" directo en Page** (sin Border wrapper)  
❌ **Depender solo de Margin para separación visual** (insuficiente)  

---

### Resultado UX

El usuario diferencia inmediatamente:  
✅ Documentos abiertos (Workspace tabs: más altos, background sutil, dominantes)  
✅ Navegación de módulo (Module tabs: compactos, transparentes, secundarios)  
✅ Sin ensimamiento visual ni caos de tabs  
✅ Experiencia limpia, estable, profesional, ERP-like, operacional  

---

## Relación Comercial Bajo Demanda — ADR-038 (implementado 2026-05-11)

### Regla de dominio institucional

`RelacionComercial` es un **vínculo comercial operativo/transaccional**. NO es un catálogo maestro de UI ni un directorio de búsqueda.

| Entidad | Rol institucional |
|---|---|
| `core.Persona` | Source of truth — personas físicas y contactos |
| `core.EmpresaComercial` | Source of truth — empresas externas / personas morales |
| `core.RelacionComercial` | Vínculo operativo — solo existe cuando hay transacción real |

### Arquitectura implementada

**Capa Application**:
- `DirectorioSelectorDto` + `DirectorioEntityType` → DTO institucional del Directorio
- `IDirectorioService.BuscarParaSelectorAsync` → búsqueda directa Persona + EmpresaComercial
- `DirectorioService` → implementación con `IgnoreQueryFilters()`, filtros explícitos de empresa/activo
- `IRelacionComercialService.GetOrCreateAsync` → patrón bajo demanda: reutiliza o crea en el momento de guardar

**Capa WinUI**:
- `RelacionComercialSelectorControl` → migrado a `IDirectorioService` / `DirectorioSelectorDto`
- `CotizacionDocumentoViewModel`, `PedidoDocumentoViewModel`, `VentaDocumentoViewModel`, `OrdenTrabajoDocumentoViewModel` → guardan `DirectorioSelectorDto?` y resuelven `RelacionComercialId` en `GuardarAsync`

### Flujo de datos

```
Usuario busca "constructora"
    → IDirectorioService.BuscarParaSelectorAsync
        → Persona WHERE NombreCompleto LIKE + EmpresaComercial WHERE RazonSocial LIKE
        → retorna List<DirectorioSelectorDto>  [sin RelacionComercial preexistente]
    ↓
Usuario selecciona EmpresaComercial "Constructora XYZ"
    → ViewModel._entidadSeleccionada = DirectorioSelectorDto
    ↓
Usuario presiona Guardar
    → GetOrCreateAsync(empresaId, dto, usuarioId)
        → ¿Existe RelacionComercial con ese EmpresaComercialId?
            SI → reutilizar Id
            NO → INSERT RelacionComercial automático
    → Documento se persiste con RelacionComercialId resuelto
```

### Anti-patterns prohibidos (ADR-038)

❌ Selector que busca en `RelacionComercial` (catálogo masivo)  
❌ Scripts de normalización masiva preventiva  
❌ `RelacionComercial` requerida para que una entidad aparezca en la UI  
❌ Sincronización artificial Directorio ↔ `RelacionComercial`  
❌ `normalizacion_relacion_comercial.sql` — **DESHABILITADO**, ver script  

### Estado

✅ **IMPLEMENTADO Y BUILD EXITOSO** — Selector funcional sin `RelacionComercial` preexistente. GetOrCreate transparente al guardar. Scripts de normalización obsoletos.

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
| Dashboard light dinámico (indicadores runtime) | Media | `IInventarioService` + queries agregadas |
| Navegación Entrada → Proveedor / OrdenCompra | Baja | Compras UI pendiente |

