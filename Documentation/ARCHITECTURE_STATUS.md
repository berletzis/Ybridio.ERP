# Architecture Status — Ybridio ERP

> Última actualización: 2026-05-09 → **Document Surface UX Standardization** (Piloto Cotizaciones: reemplazar Workspace Tabs innecesarios con Document Surfaces contextuales embebidos para CRUDs ligeros)  
> Build: ✅ 0 errores | BD: YBRIDIO-26 | Docs relacionados: `DECISIONS.md` · `ROADMAP.md` · `KNOWN_ISSUES.md` · `CLAUDE_RULES.md`  
> Fix crítico: `IdentityRole<Guid>` → `_context.Roles` en PermisoService (KI-012, ADR-014)

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

