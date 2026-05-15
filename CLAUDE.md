# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Documentación obligatoria — leer ANTES de cualquier implementación

Al iniciar cada sesión o antes de proponer/implementar cualquier cambio, leer en este orden:

1. `Documentation/CLAUDE_RULES.md` — reglas maestras de arquitectura (ADRs, patrones, anti-patterns)
2. `Documentation/ARCHITECTURE_STATUS.md` — estado actual de módulos, capas y decisiones vigentes
3. `Documentation/SESSION_*.md` más reciente — qué se hizo en la última sesión y próximos pasos
4. `Documentation/KNOWN_ISSUES.md` — problemas conocidos activos que no deben reproducirse

Estos archivos son la fuente de verdad operacional del proyecto. `CLAUDE.md` es el resumen estructural; los detalles vivos están en `Documentation/`.

---

## Session Closure Governance Policy

### Trigger

Cuando el usuario solicite:

- `Ejecutar Session Closure Review`
- `Actualizar artefactos institucionales`

Claude Code DEBE ejecutar el proceso completo descrito a continuación.

---

### Proceso obligatorio

#### 1. Análisis de impacto

Antes de actualizar cualquier artefacto, analizar:

| Dimensión | Preguntas a responder |
|---|---|
| **Arquitectónico** | ¿Se agregaron entidades, servicios, capas, patrones? ¿Algún ADR fue implícitamente creado? |
| **Workflow** | ¿Cambió el lifecycle de algún documento (COT/PED/VTA/OT)? ¿Nuevos estados? ¿Nuevas transiciones? |
| **Runtime** | ¿Nuevas dependencias DI? ¿Cambios de lifetime? ¿Riesgo de concurrencia o cancelación? |
| **Auditoría estructural** | ¿Se agregaron columnas, tablas, enums, FK? ¿El auditor generará falsos positivos con los cambios? |
| **Legacy** | ¿Los registros existentes son compatibles con los cambios? ¿Se necesita script de backfill? |

---

#### 2. Artefactos obligatorios a evaluar y actualizar

| Artefacto | Actualizar cuando... |
|---|---|
| `Documentation/ARCHITECTURE_STATUS.md` | Se implemente cualquier módulo, patrón o ADR nuevo |
| `Documentation/DECISIONS.md` | Se tome una decisión arquitectónica (nuevo ADR) |
| `Documentation/KNOWN_ISSUES.md` | Se resuelva un KI existente o se detecte uno nuevo |
| `Documentation/CLAUDE_RULES.md` | Se formalice un nuevo patrón, anti-pattern o regla de arquitectura |
| `Documentation/SESSION_*.md` | Siempre — registrar qué se hizo, estado actual y próximos pasos |

---

#### 3. Sección obligatoria: Impacto en Auditoría Estructural

Si la sesión incluye cualquiera de los siguientes cambios:

- Nuevas columnas en tablas existentes
- Nuevas tablas o schemas
- Cambios en enums de workflow (EstatusCotizacion, EstatusPedido, EstatusVenta)
- Cambios en lifecycle o conversiones documentales
- Cambios en snapshots (NombreCliente, Descripcion, Importe en detalles)
- Nuevos scripts SQL manuales
- Cambios en persistencia de totales o pagos

Claude Code DEBE incluir en el SESSION_*.md la sección:

```
## Impacto en Auditoría Estructural

### Nuevas validaciones requeridas
- (lista de validadores a agregar o actualizar en WorkflowAuditService / CommercialIntegrityAuditService)

### Validaciones obsoletas o que generan falsos positivos
- (validadores que deben desactivarse, reclasificarse o actualizarse)

### Riesgo de falsos positivos
- (qué generará Critical/Error incorrectos en SchemaAuditService tras los cambios)

### Impacto legacy
- (registros existentes que tendrán valores null/default, esperados como LegacyData)

### Migraciones requeridas
- (scripts SQL necesarios, columnas faltantes, constraints pendientes)

### Recalibración de severidades
- (si algún finding existente debe subir/bajar de severidad por el nuevo contexto)
```

---

#### 4. Restricciones críticas (PROHIBIDO en Session Closure)

- ❌ Reparar findings de auditoría automáticamente sin análisis previo
- ❌ Reclasificar severidades sin justificación arquitectónica documentada
- ❌ Modificar datos legacy sin validación explícita del usuario
- ❌ Asumir corrupción de datos cuando puede ser estado legacy válido
- ❌ Actualizar CLAUDE_RULES.md con reglas que contradicen ADRs existentes
- ❌ Cerrar KI sin confirmar que el fix funciona en runtime

---

#### 5. Objetivo institucional

El Session Closure Review garantiza que al terminar cada sesión significativa:

1. El código, la arquitectura, el runtime y el workflow estén sincronizados
2. Los ADRs reflejen las decisiones reales tomadas (no solo las planeadas)
3. El módulo de Auditoría Estructural pueda ejecutarse sin falsos positivos
4. Los KI estén actualizados con issues reales y resueltos marcados correctamente
5. La siguiente sesión pueda empezar con contexto completo y sin deuda documental

**Frase clave a evitar:** *Architecture Drift* — cuando el código evoluciona pero la documentación no.

---

## Build & Run

```powershell
# Build (x64 debug)
dotnet build Ybridio.WinUI/Ybridio.WinUI.csproj -p:Platform=x64

# Build release
dotnet build Ybridio.WinUI/Ybridio.WinUI.csproj -p:Platform=x64 -c Release

# Run (requires MSIX packaging or unpackaged deploy in VS)
# Use Visual Studio 2022 with Windows App SDK workload — F5 deploys via MSIX tooling.
# dotnet run does NOT work for WinUI 3 projects; use VS or msix deploy.

# Restore
dotnet restore Ybridio.ERP.slnx
```

No unit test projects exist yet.

## Global visual style (Outlook 2026)

- **Typography**: `ErpCellFontSize=14`, `ErpHeaderFontSize=13`, `ErpRowHeight=56` — defined in `Styles/DataGrid/DataGridBase.xaml`
- **DataGrid container**: wrap the ListView in `<Border Margin="20,8,20,0" Background="White" BorderBrush="#E5E5E5" BorderThickness="1">` — consistent across all modules
- **Tabs**: `OutlookTabItemStyle` in `App.xaml` — FontSize=14, MinHeight=40, blue underline indicator, SemiBold when active
- **Shell TopBar**: shows only section title + sucursal/caja + user; search lives inside each module page

## Architecture

Clean Architecture in 4 layers (bottom → top):

```
Ybridio.Domain          → Entities, AuditableEntity base, no dependencies
Ybridio.Infrastructure  → EF Core 8 + SQL Server + ASP.NET Identity
Ybridio.Application     → Service interfaces + implementations + DTOs
Ybridio.WinUI           → WinUI 3 presentation (Windows App SDK 2.0.1)
```

### Domain (`Ybridio.Domain`)
- All entities inherit `AuditableEntity` (FechaCreacion, FechaModificacion, Borrado soft-delete, RowVersion concurrency token)
- Namespaced by business domain: `Catalogos/`, `Inventario/`, `Ventas/`, `Finanzas/`, `Compras/`, `Seguridad/`
- Multi-tenant: every major entity has `EmpresaId` (scopes all queries)

### Infrastructure (`Ybridio.Infrastructure`)
- `ErpDbContext` is the single DbContext (Scoped lifetime)
- Identity via `ApplicationUser` / `ApplicationRole` on the same context
- EF configs in `Persistence/Configurations/`
- Connection string is hardcoded in `App.xaml.cs` (dev environment only)
- **Global soft-delete filter**: `OnModelCreating` applies `!Borrado` automatically to all `AuditableEntity` and `CreationAuditEntity` subclasses. Plain entities (e.g., `ProductoCategoria`) have NO filter.

### DB Schema layout

Schemas after reclassification:

```
core/           → business entities (transactional / operational)
  Empresa, Sucursal, Cliente, Proveedor
  Producto, ProductoCategoria, ProductoSucursal
  (previously: Tienda renamed to Sucursal; Producto/Cliente/Proveedor moved from catalogos)

catalogos/      → reference / lookup data only
  CategoriaProducto, TipoProducto, TipoImpuesto, UnidadMedida
  Moneda, FormaPago, MetodoPago, TipoDocumento, EstatusGeneral
  Pais, Estado, Ciudad

inventario/     → Almacen, Existencia, MovimientoInventario, TipoMovimientoInventario
finanzas/       → Caja, AperturaCaja, MovimientoCaja, TipoMovimientoCaja
ventas/         → Venta, VentaDetalle, Factura
compras/        → OrdenCompra, OrdenCompraDetalle, RecepcionCompra, RecepcionCompraDetalle
seguridad/      → Usuario, Rol, UsuarioSucursal, UsuarioPermiso, Modulo, Permiso, RolPermiso
                   Perfil, PerfilPermiso, UsuarioPerfil, UsuarioAlmacen   ← Security Foundation (added 2026-05-07)
                   + Identity tables (UsuarioClaim, UsuarioLogin, UsuarioToken, UsuarioRol, RolClaim)
```

**Rule**: never add transactional/business tables to `catalogos`. Only lookup/reference data belongs there.

### Domain — Tienda → Sucursal (completed rename)

The entity formerly known as `Tienda` is now `Sucursal` everywhere:
- `Ybridio.Domain.Core.Sucursal` (was `Tienda`)
- `ISucursalService` / `SucursalService` / `SucursalDto` (was `ITiendaService` etc.)
- `SessionService.SucursalId`, `SucursalNombre`, `SucursalChanged` event
- `BaseContextViewModel` subscribes to `SucursalChanged` via `HandleSucursalChanged`
- `ShellViewModel.SucursalesDisponibles`, `SeleccionarSucursalCommand`
- DB table: `core.Sucursal` (was `core.Tienda`)
- Join table: `core.ProductoSucursal` (was `catalogos.ProductoTienda`)
- Security table: `seguridad.UsuarioSucursal` (was `seguridad.UsuarioTienda`)

### Domain — Producto↔Categoría (N:N)
`Produto` no longer has a direct `CategoriaId` FK. The relationship is **many-to-many via `ProductoCategoria`**:

```
core.Produto                   ← moved from catalogos
catalogos.CategoriaProducto    ← self-referencing via CategoriaPadreId (hierarchy) — stays in catalogos
core.ProductoCategoria         ← join table: Id, ProductoId, CategoriaId, EsPrincipal, FechaCreacion
```

- `ProductoCategoria` is NOT an `AuditableEntity` (no `Borrado` column) — no global filter applies
- `EsPrincipal=true` marks the "main" category shown in list views
- `CategoriaProducto.CategoriaPadreId` enables hierarchical trees (FK_CategoriaProducto_Padre)

### Application (`Ybridio.Application`)
- Services registered via `AddApplicationServices()` extension — all **Scoped**
- `ServiceResult<T>` / `ServiceResult` return type for all write operations (has `.Success`, `.Value`, `.Error`, `.ErrorCode`)
- DTOs are `sealed record` types in `DTOs/` subfolders matching domain namespaces
- `ErrorCode` enum for typed error mapping in the UI
- New folder `Services/Autorizacion/` — security foundation runtime services

**`ProductoDto`** has two category fields:
- `CategoriaId` / `CategoriaNombre` — the *principal* category (EsPrincipal=true), for display
- `CategoriaIds: IReadOnlyList<int>` — ALL category IDs the product belongs to, for N:N filtering in the ViewModel

**`ProductoService` query pattern**: navigation properties require `Include()`; `MapToDto` runs client-side after `ToListAsync()`. Do NOT use `.Select(p => MapToDto(p))` before `ToListAsync` — EF Core won't translate the method and navigation properties will be null. Always:
```csharp
var lista = await query.Include(...).ToListAsync(ct);
return lista.Select(MapToDto).ToList();
```

### WinUI (`Ybridio.WinUI`)
- **MVVM**: CommunityToolkit.Mvvm 8.4.0 — `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`
- **DI**: `Microsoft.Extensions.DependencyInjection` via `App.Services` static property
- **Navigation**: `INavigationService` (singleton) wraps a WinUI `Frame`; prevents duplicate navigation to the same page type
- **Session**: `SessionService` (singleton) holds logged-in user, sucursal activa (`SucursalId`, `SucursalNombre`), caja activa. Event `SucursalChanged` notifica cambio de sucursal.
- **ISessionContext**: exposes `EmpresaId`, `SucursalId`, **and `UsuarioId` (Guid?)**. `UsuarioId` is consumed by Application-layer security services — do not bypass it.

## Key Patterns

### Page + ViewModel wiring
**Always set `ViewModel` BEFORE `InitializeComponent()`** — x:Bind compiled bindings evaluate during `InitializeComponent`. Setting ViewModel after causes all Mode=OneWay bindings to evaluate with null and not update until the next PropertyChanged fires.

```csharp
public sealed partial class MyPage : Page
{
    public MyViewModel ViewModel { get; }
    public MyPage()
    {
        ViewModel = App.Services.GetRequiredService<MyViewModel>();
        InitializeComponent();   // ← AFTER ViewModel is assigned
    }
}
```

### ViewModels
- `sealed partial class` inheriting `ObservableObject`
- Async commands: `[RelayCommand] private async Task FooAsync()` → generates `FooCommand` (Async suffix stripped)
- CanExecute: `[RelayCommand(CanExecute = nameof(GuardMethod))]`; call `FooCommand.NotifyCanExecuteChanged()` when guard state changes
- Fire-and-forget partial hooks: `partial void OnPropertyChanged(T value) => _ = SomeAsync();`

### Resource keys (WinUI 3 — NOT UWP)
Use WinUI 3 theme brush names. UWP `SystemControl*` keys throw `XamlParseException` at runtime:

| ❌ UWP (breaks) | ✅ WinUI 3 |
|---|---|
| `SystemControlBackgroundChromeLowBrush` | `LayerFillColorDefaultBrush` |
| `SystemControlBackgroundChromeMediumBrush` | `LayerFillColorDefaultBrush` |
| `SystemControlHighlightAccentBrush` | `AccentFillColorDefaultBrush` |
| `SystemControlForegroundBaseMediumBrush` | `TextFillColorSecondaryBrush` |
| `SystemControlForegroundBaseHighBrush` | `TextFillColorPrimaryBrush` |

### Thickness in WinUI 3
`Thickness` has **only a 4-argument constructor** (`left, top, right, bottom`). WPF's 1- and 2-argument constructors do not exist:
```csharp
new Thickness(8, 4, 8, 4)   // ✓
new Thickness(8, 4)          // ✗ compile error
new Thickness(8)             // ✗ compile error
```

### Programmatic Windows (no XAML)
Secondary windows (detail forms, comparison views) are built entirely in C# — no `.xaml` file. Pattern:
```csharp
public sealed class MyWindow : Window
{
    public MyWindow()
    {
        AppWindow.Resize(new SizeInt32(w, h));
        // Center over main window:
        var main = App.Services.GetRequiredService<MainWindow>();
        main.Closed += (_, _) => this.Close();   // cascade close
        var p = main.AppWindow.Position; var ms = main.AppWindow.Size; var ts = AppWindow.Size;
        AppWindow.Move(new PointInt32(p.X + (ms.Width - ts.Width) / 2, p.Y + (ms.Height - ts.Height) / 2));
        Content = BuildUI();
    }
}
```
- `Grid.SetColumn/Row` require `FrameworkElement`, not `UIElement` — declare helper parameters as `FrameworkElement`
- `Application.Current` is ambiguous with `Ybridio.Application`; use alias: `using XamlApp = Microsoft.UI.Xaml.Application;`

### Adding a new module (full checklist)
1. Domain entity inheriting `AuditableEntity`
2. EF Core configuration in `Infrastructure/Persistence/Configurations/`
3. DTOs (`sealed record`) in `Application/DTOs/`
4. Service interface + implementation in `Application/Services/`; register in `ServiceCollectionExtensions.AddApplicationServices()`
5. ViewModel in `WinUI/ViewModels/<Module>/`; register `services.AddTransient<XxxViewModel>()` in `App.xaml.cs`
6. XAML page in `WinUI/Views/<Module>/`; register `services.AddTransient<XxxPage>()` in `App.xaml.cs`
7. Add `<None Remove>` + `<Page Update Generator="MSBuild:Compile">` entries in `Ybridio.WinUI.csproj`
8. **Every module needs a placeholder page** — if `SelectModule(modulo)` does not call `_navigation.NavigateTo(typeof(XxxPage))`, the Frame retains the last loaded page
9. Add navigation case in `ShellViewModel.SelectModule`

### ClassificationPanel — control reutilizable

Ubicación: `Ybridio.WinUI/Controls/Navigation/ClassificationPanel.xaml`

**DependencyProperties:**

| DP | Tipo | Descripción |
|---|---|---|
| `ItemsSource` | `IEnumerable<ClassificationItem>` | Árbol de nodos — asignado al `TreeView.ItemsSource` directamente |
| `SelectedItem` | `ClassificationItem` | Nodo actualmente seleccionado |
| `PanelTitle` | `string` | Título mostrado en el header (e.g., `"Categorías"`) |
| `IsFilterActive` | `bool` | Controla el ToggleButton del header (⊟ icono de filtro) |

**Evento:** `SelectionChanged(object sender, ClassificationItem? e)`

**Reglas de uso:**
- `ClassTree.ItemsSource` = el `ObservableCollection` directamente — NO usar `RootNodes` API (el DataContext del DataTemplate es el `TreeViewNode`, no el `Content`, causando bindings vacíos)
- DataTemplate raíz = `<TreeViewItem ItemsSource="{x:Bind Children}">` — jerarquía N niveles automática
- Usar `{x:DataType}` + `{x:Bind}` en DataTemplate (NO `{Binding}` sin `x:DataType`)
- `ClearSelection()`: deselecciona el nodo activo (llamar desde la Page cuando el filtro se limpia)

**`ClassificationItem`** — `Controls/Navigation/ClassificationItem.cs`:
```
Id (string)  Name (string)  Count (int)  CountDisplay (computed)
CategoriaId (int?)  IsRoot (bool)  IsExpanded (bool)  Children (ObservableCollection<ClassificationItem>)
```

### Módulo Productos — layout de panel colapsable

`ProductosPage.xaml` usa un **Grid de 3 columnas** (no SplitView):
```
Col 0 (0px→240px): ClassificationPanel  [MinWidth=0, MaxWidth=400]
Col 1 (0px→5px):   Border de resize (drag handle)
Col 2 (*):          Contenido (search + chip + lista + statusbar)
```

- Hamburger button (☰) → `TogglePanel_Click` → alterna `Col[0].Width` entre 0 y 240px
- Drag handle → `PanelResize_PointerPressed/Moved/Released` → `Math.Clamp(180..400)`
- Chip de filtro activo: `Border` en Row 1 del contenido; `Visibility` enlazada a `ViewModel.FiltroActivoVisibility`; botón ✕ llama `ViewModel.LimpiarFiltro()` + `ClasificacionPanel.ClearSelection()`

**Filtrado jerárquico en ViewModel:**
```csharp
// Al seleccionar un nodo, calcular IDs del nodo + todos sus descendientes
_categoriaFiltroIds = GetAllCategoryIds(item);   // HashSet<int>
// En AplicarFiltro:
lista.Where(p => p.CategoriaIds.Any(id => _categoriaFiltroIds.Contains(id)))
```

**`FiltroLimpiadoCallback`**: `Action?` en el ViewModel que la Page asigna en `OnNavigatedTo` para deseleccionar el panel sin acoplar ViewModel a la Vista.

### Shell navigation
`ShellViewModel.SelectModule` must:
1. Collapse **all** `ShowRibbonXxx` properties first
2. Set the target module's ribbon to `Visible`
3. Call `_navigation.NavigateTo(typeof(XxxPage))` — every module case must navigate, even placeholders

`MainWindow` is registered as **singleton** (`services.AddSingleton<MainWindow>()`); `OnLaunched` retrieves it via `Services.GetRequiredService<MainWindow>()`.

### Shell sidebar (Outlook style)

The shell sidebar uses icon-on-top + label-below buttons (72×64px, transparent background) in a `Grid` named `NavButtonsPanel`. The Grid uses a `*` spacer row to push config buttons to the bottom:

```
[Dashboard] [POS] [Inventario] [Ventas] [Contactos]
            ────────── spacer * ──────────
            ──── separator 1px ────
[Global]  [Sucursal]  [Salir]
```

- `ModuleButton_Click` → `ViewModel.SelectModuleCommand.Execute(tag)`
- `SetActiveNavButton(btn)` iterates `NavButtonsPanel.Children` and highlights the active button
- Tags for config: `"ConfiguracionGlobal"` → `ConfiguracionPage` with param `"Global"` | `"ConfiguracionTienda"` → param `"Tienda"` (legacy tag — kept for nav routing, opens Sucursal config tabs)

### ConfiguracionPage — dual-mode (Global / Sucursal)

`ConfiguracionPage` shows two different `TabView` sets based on the navigation parameter:

| Parámetro | TabView visible | Tabs |
|---|---|---|
| `"Global"` (default) | `TabsGlobal` | Empresa · Sucursales · Auditoría del Sistema · Seguridad |
| `"Tienda"` | `TabsTienda` | Usuarios · Cajas · Dispositivos · Promociones · Almacenes · Permisos · Facturación · Personalización |

`NavigationService` allows re-navigation to the same page when a parameter is passed (guard bypassed). The page determines visibility in `OnNavigatedTo`.

### Audit services

Two independent audit services in `Ybridio.Infrastructure.Persistence.Audit/`:

**`ISchemaAuditService`** — compares EF model vs actual DB schema (structural audit):
- Detects missing tables, missing columns, type mismatches, pending migrations, orphan FK constraints
- Severities: `Critical` (FK/type mismatch) · `Error` (missing table/column) · `Warning` (orphan) · `Info`

**`IDatabaseAuditService`** — validates data integrity post-migration (data audit):
- `GetInvalidForeignKeysAsync()` — FK integrity in `core.Produto` (TipoProductoId, UnidadMedidaId, TipoImpuestoId)
- `GetOrphanRecordsAsync()` — migmap tables integrity
- `GetLegacyDependenciesAsync()` — FK constraints still pointing to `dbo` schema
- `GetUnmigratedRecordsAsync()` — dbo records without migmap entry
- `GetDuplicateCatalogsAsync()` — duplicate Nombre/Clave per catalog
- `GetDataIssuesAsync()` → hierarchy, impuesto % range, abreviatura truncation, geography

Both services are registered `Transient` and exposed in **Config Global → Auditoría del Sistema** tab.

SQL queries in `DatabaseAuditService` use **schema-qualified table names** that must match the actual DB:
- `core.Produto` (NOT `catalogos.Produto` — moved!)
- `catalogos.TipoProducto`, `catalogos.CategoriaProducto` — stay in catalogos

---

### Security Foundation Runtime Architecture (added 2026-05-07)

RBAC + Profiles + Security Context Scopes construido sobre ASP.NET Identity existente.
Ver `docs/SECURITY_FOUNDATION.md` para documentación completa.

#### Modelo de evaluación de permisos (3 niveles, en orden de prioridad)

```
1. UsuarioPermiso.Permitido = true/false  → override explícito (gana siempre)
                              null        → hereda hacia abajo
2. UsuarioPerfil → PerfilPermiso          → perfiles asignados al usuario
3. UsuarioRol   → RolPermiso              → herencia desde roles
```

Un denegado explícito (`Permitido = false`) veta cualquier permiso de los niveles inferiores.

#### Nuevas entidades de dominio (`Ybridio.Domain/Seguridad/`)

| Entidad | Tabla | Notas |
|---|---|---|
| `Perfil` | `seguridad.Perfil` | Hereda `CreationAuditEntity` — soft-delete global aplicado |
| `PerfilPermiso` | `seguridad.PerfilPermiso` | Join table sin soft-delete (eliminación directa) |
| `UsuarioPerfil` | `seguridad.UsuarioPerfil` | Join table sin soft-delete |
| `UsuarioAlmacen` | `seguridad.UsuarioAlmacen` | Scope de almacén; sin almacenes = accede a todos los de sus sucursales |

#### Servicios Application nuevos (`Services/Autorizacion/`)

| Interfaz | Propósito |
|---|---|
| `IErpAuthorizationService` | Motor principal: `PuedeAsync(clave)`, `ObtenerContextoSeguridad()` |
| `ISecurityContextService` | Snapshot completo: roles + perfiles + permisos + scopes |
| `ISecurityScopeResolver` | Qué sucursales/almacenes puede ver el usuario; `EsSuperAdminAsync()` |
| `IPerfilService` | CRUD de perfiles + asignación a usuarios |

`MemoryPermissionCache` (Singleton, TTL 10 min) reemplaza `NullPermissionCache`. Invalidar tras cambio de rol/permiso: `await _cache.InvalidateAsync(userId)`.

#### PermisosClave — constantes tipadas (`Common/PermisosClave.cs`)

```csharp
// SIEMPRE usar constantes. NUNCA strings literales en código.
await _auth.PuedeAsync(PermisosClave.Salida.Autorizar);   // "salida.autorizar"
await _auth.PuedeAsync(PermisosClave.Venta.Crear);        // "venta.crear"
await _auth.PuedeAsync(PermisosClave.Caja.Abrir);         // "caja.abrir"
```

Categorías: `Venta`, `Entrada`, `Salida`, `Traspaso`, `Ajuste`, `Existencia`, `Producto`, `Caja`, `Compra`, `Cliente`, `Proveedor`, `Configuracion`, `Seguridad`, `Reporte`.

#### Regla crítica de autorización

```
NUNCA:   if (rol == "Admin") { ... }
SIEMPRE: if (await _auth.PuedeAsync(PermisosClave.Xxx.Yyy)) { ... }
```

#### Scopes de seguridad (SecurityScopeResolver)

- **SuperAdmin** (rol): bypass total de scopes sucursal/almacén
- `UsuarioSucursal`: lista de sucursales permitidas (vacía en SuperAdmin = sin restricción)
- `UsuarioAlmacen`: lista de almacenes permitidos; vacía = todos los almacenes de sus sucursales

#### Datos seed en BD (seguridad schema)

- **10 módulos**: ventas, inventario, caja, compras, productos, clientes, proveedores, configuracion, seguridad, reportes
- **51 permisos** con claves formato `entidad.accion`
- **8 roles**: SuperAdmin (51), AdministradorEmpresa (50), GerenteSucursal (33), SupervisorInventario (25), SupervisorVentas (11), Cajero (10), OperadorInventario (8), Vendedor (7)
- **5 perfiles**: POS Básico, Inventario Operativo, Inventario Supervisor, Ventas Operativo, Ventas Supervisor

#### Observabilidad integrada

`RuntimeDiagnosticService.GetSnapshot(securityCtx?)` acepta `SecurityContextDto?` opcional.
Cuando se provee, el snapshot incluye `SecurityRuntimeSnapshot` (roles, perfiles, permisos count, scopes, EsSuperAdmin).

---

## Conventions (obligatorias en todo código nuevo)

### Naming

| Elemento | Regla | Ejemplo |
|---|---|---|
| Clases | PascalCase | `ProductoService`, `UsuarioDto` |
| Interfaces | Prefijo `I` + PascalCase | `IProductoService`, `ISucursalService` |
| Métodos | PascalCase; async → sufijo `Async` | `ObtenerProductosAsync`, `GuardarAsync` |
| Propiedades | PascalCase | `FechaCreacion`, `EmpresaId` |
| Campos privados | `_camelCase` | `_session`, `_roleManager` |
| Parámetros / variables locales | camelCase | `productoId`, `cancellationToken` |

### Folder structure

```
Ybridio.Domain/
  Catalogos/   Inventario/   Ventas/   Finanzas/   Compras/   Seguridad/   Core/   Common/

Ybridio.Application/
  DTOs/<DomainArea>/
  Services/<DomainArea>/   (interface + implementation)
  Common/                  (ServiceResult, ErrorCode, etc.)
  Extensions/

Ybridio.Infrastructure/
  Persistence/
    Configurations/<DomainArea>/
    Migrations/
    Identity/
  Extensions/

Ybridio.WinUI/
  Views/<Module>/
  ViewModels/<Module>/
  Controls/
  Helpers/
  Services/    ← solo UI helpers (NavigationService, WindowManager)
  Styles/
```

### Mandatory patterns

- **Business logic → Application layer only.** Never in WinUI ViewModels or Infrastructure.
- **DTOs between layers.** Never expose domain entities to WinUI.
- **ViewModels are orchestrators.** Call Application services, map to observable properties, fire commands. No SQL, no EF, no Identity APIs.
- **Services → Scoped lifetime.** Register in `ServiceCollectionExtensions.AddApplicationServices()`.
- **ViewModels → Transient lifetime.** Register in `App.xaml.cs`.

### XML Documentation (required on all public API)

Every `public` or `internal` class, interface, method, property, and constructor in **Domain**, **Application**, and **Infrastructure** must carry XML doc comments. WinUI ViewModels and Pages are also documented when they expose public members used by XAML bindings.

```csharp
/// <summary>
/// Obtiene todos los productos activos de la empresa en sesión,
/// aplicando el filtro de soft-delete global.
/// </summary>
/// <param name="ct">Token de cancelación.</param>
/// <returns>Lista inmutable de <see cref="ProductoDto"/> ordenada por nombre.</returns>
public async Task<IReadOnlyList<ProductoDto>> ListarAsync(CancellationToken ct = default)

/// <summary>
/// Crea un nuevo producto y lo asocia a la categoría principal indicada.
/// </summary>
/// <param name="dto">Datos del producto a crear.</param>
/// <param name="usuarioId">ID del usuario que realiza la operación (auditoría).</param>
/// <param name="ct">Token de cancelación.</param>
/// <returns>
/// <see cref="ServiceResult{T}"/> con el <see cref="ProductoDto"/> creado,
/// o error <see cref="ErrorCode.ValidationFailed"/> si los datos son inválidos.
/// </returns>
public async Task<ServiceResult<ProductoDto>> CrearAsync(
    CrearProductoDto dto, Guid usuarioId, CancellationToken ct = default)
```

**What to document per type:**

| Type | Required in `<summary>` |
|---|---|
| Service / interface | What it does, which business rules it enforces, which context (Empresa/Sucursal) it uses |
| DTO / record | Purpose, which operation it belongs to, nullable semantics |
| Entity | Business meaning, key relationships, soft-delete / audit behavior |
| ViewModel | Which page it serves, which events it reacts to (e.g. `SucursalChanged`) |
| Migration | Why it exists (drift fix, new feature, schema change) |

**Rules:**
- Use `<see cref="..."/>` for type references inside comments.
- Omit `<returns>` only on `void` / `Task` methods with no meaningful result.
- One-sentence `<summary>` is enough if the method name is already self-describing; expand only when the WHY or the constraints are non-obvious.
- Do NOT document `<param>` for `CancellationToken ct` — it is self-evident.
- Private methods: document only when the logic is non-obvious (algorithm, workaround, constraint).
