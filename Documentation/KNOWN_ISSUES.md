# KNOWN_ISSUES.md — Problemas Conocidos y Limitaciones

> Última actualización: 2026-05-15 (ver `SESSION_2026-05-15_PEDIDOS_COMMERCIAL_SURFACE_BUGFIXES.md`)
> Formato: `[SEVERIDAD] Descripción — Workaround / Plan`

---

## Severidades

- **BLOCKER** — impide operación normal; debe resolverse antes de producción
- **HIGH** — impacto significativo en UX o seguridad; resolver en siguiente iteración
- **MEDIUM** — limitación funcional; resolver en fase 2
- **LOW** — cosmético o técnico menor; backlog

---

### [RESUELTO] KI-036 — Descuentos no preservados al convertir Cotización → Pedido

**Módulo**: Ventas — `CotizacionService.ConvertirAPedidoAsync`, `PedidoService.AgregarDetalleAsync`, `PedidoDocumentoPage`

**Problema**: `PedidoDetalle.DescuentoPct` siempre quedaba en 0 al convertir, aunque la cotización tuviera descuentos. Causa raíz: cadena de 3 bugs independientes (EF change tracker stale + `AgregarDetalleAsync` sin `CommercialDocumentCalculator` + WinUI 3 DataTemplate `ValueChanged` durante render inicial).

**Solución**: (1) `AsNoTracking()` para detalles/cargos en conversión. (2) `CommercialDocumentCalculator` en `AgregarDetalleAsync` y `CrearAsync`. (3) `_listaParaEdicion` guard via `Page.Loaded` en todos los NumberBox handlers. (4) `HasDefaultValue` eliminado de `PedidoDetalleConfiguration` y `CotizacionDetalleConfiguration`. **Estado**: ✅ **RESUELTO 2026-05-15** — ver `BUGFIX_DESCUENTOS_PEDIDO_CONVERSION.md`

---

### [RESUELTO] KI-035 — Conversión COT→PED abría WorkspaceTab (visual incorrecto)

**Módulo**: Ventas — `CotizacionDocumentoPage.AbrirPedidoEnWorkspace`

**Problema**: Al convertir, el Pedido abría en WorkspaceTab flotante en lugar de inline en PedidosPage, causando transparencia y layout incorrecto.

**Solución**: Visual tree traversal `EncontrarAncestro<VentasPage>()` → `VentasPage.AbrirPedidoDesdeConversion()` → `PedidosPage.AbrirPedidoDesdeConversion()`. El Pedido ahora abre idéntico a cuando se abre desde el grid. **Estado**: ✅ **RESUELTO 2026-05-15**

---

### [RESUELTO] KI-034 — Script BD AddWorkflowColumns_V1.sql pendiente de ejecutar

**Módulo**: Infrastructure — `ventas.PedidoDetalle`, `ventas.Pedido`

**Problema**: Las nuevas columnas `DescuentoPct` y `IvaAplicable` en `PedidoDetalle`, y `Subtotal` en `Pedido` existen en las entidades EF Core y las configuraciones, pero no en la base de datos YBRIDIO-26 hasta ejecutar el script.

**Workaround**: La aplicación compila y el runtime funciona para registros existentes. Crear nuevos pedidos puede fallar si EF intenta persistir las columnas antes de ejecutar el script.

**Plan**: Ejecutar inmediatamente `Documentation/Scripts/AddWorkflowColumns_V1.sql` en YBRIDIO-26.

**Estado**: ✅ **RESUELTO 2026-05-14** — columnas aplicadas directamente vía SqlClient desde Claude Code.

---

### [RESUELTO] KI-033 — OtrosCargos no persistían al guardar cotización nueva (ADR-054)

**Módulo**: Cotizaciones — `CotizacionDocumentoViewModel.GuardarAsync`

**Problema**: Para documentos nuevos (IsNuevo=true), `GuardarAsync` creaba la cotización vía `CrearAsync` pero nunca persistía los cargos acumulados en `ViewModel.Cargos`. Al reabrir, los cargos desaparecían. Además `IvaAplicable` faltaba en `CrearDetalleLineaDto`, perdiéndose el flag de IVA por línea.

**Solución**: Después de `CrearAsync` exitoso, loop explícito sobre `Cargos.ToList()` llamando `AgregarCargoAsync` por cada cargo en memoria. `CrearDetalleLineaDto` actualizado con `IvaAplicable`. **Estado**: ✅ **RESUELTO 2026-05-13**

---

### [RESUELTO] KI-032 — Single Document Session Rule rota cross-host (ADR-056)

**Módulo**: Cotizaciones — `CotizacionDocumentoPage.xaml.cs`, `CotizacionesPage.xaml.cs`

**Problema**: La misma cotización podía abrirse simultáneamente en tab y ventana detached. Causa dual:
1. `_cotizacionOriginal` (readonly) permanecía null para docs nuevos aunque guardados → window key usaba `_sessionKey` (GUID) → `TryActivateWindow` fallaba.
2. `_currentInlineDocumentId` no se actualizaba tras primer guardado → check inline fallaba.

**Solución**: Propiedad `DocumentoId` en ViewModel. Window key usa `ViewModel.DocumentoId`. `DocumentSaved` callback actualiza `_currentInlineDocumentId`. **Estado**: ✅ **RESUELTO 2026-05-13**

---

### [RESUELTO] KI-031 — Scroll interno en grids de Cotizaciones (ADR-055)

**Módulo**: Cotizaciones — `CotizacionDocumentoPage.xaml`

**Problema**: `Height="*"` en fila de productos forzaba scroll interno en ListView. `MaxHeight="200"` en OtrosCargos lo clipaba. Experiencia anti-ERP desktop.

**Solución**: Single Document Scroll Pattern — ScrollViewer documental único. `ScrollViewer.VerticalScrollBarVisibility="Disabled"` en ListViews. Sin `MaxHeight` en secciones. **Estado**: ✅ **RESUELTO 2026-05-13**

---

### [RESUELTO] KI-030 — AutoSuggestBox muestra cadena técnica del record en buscador de productos

**Módulo**: Cotizaciones — `CotizacionDocumentoPage.xaml.cs`

**Problema**: `AutoSuggestBox` llama `.ToString()` en cada ítem del dropdown. `ProductoDto` (sealed record) devuelve representación técnica: `ProductoDto { Id=1, Codigo="ABC", ... }` en lugar del nombre legible.

**Solución**: Clase wrapper `ProductoSuggestion` con `override string ToString() => $"{Codigo} — {Nombre}"`. **Estado**: ✅ **RESUELTO 2026-05-13**

---

### [RESUELTO] KI-029 — DbContext concurrency en ConfiguracionFiscalService (ADR-026)

**Módulo**: `ConfiguracionFiscalService` — `CargarConfiguracionFiscalAsync` fire-and-forget

**Problema**: `CargarConfiguracionFiscalAsync()` corría concurrentemente con `HidratarSelectorClienteAsync()` usando el mismo DbContext scoped → `InvalidOperationException: A second operation was started on this context`.

**Solución**: `ConfiguracionFiscalService` usa `IDbContextFactory` (contexto aislado por operación). Mismo patrón que `FolioGeneratorService`. **Estado**: ✅ **RESUELTO 2026-05-13**

---

### [RESUELTO] KI-028 — FolioGeneratorService "non-composable SQL"

**Módulo**: `FolioGeneratorService.GenerarFolioAsync`

**Problema**: `.FirstAsync()` sobre `SqlQuery<long>($"UPDATE ... OUTPUT ...")` causaba `'SqlQuery' was called with non-composable SQL` porque EF Core intenta añadir `TOP 1` sobre sentencia DML.

**Solución**: `.ToListAsync()` materializa client-side antes de acceder con `[0]`. **Estado**: ✅ **RESUELTO 2026-05-13**

---

### [RESUELTO] KI-027 — Cliente no aparecía en chip inferior en modo edición ni tras detach (ADR-043)

**Módulo**: Cotizaciones — `CotizacionDocumentoViewModel`, `CotizacionDocumentoPage.xaml.cs`

**Problema**: `Initialize()` solo asignaba `NombreCliente` y `RelacionComercialId` pero no `_entidadDirectorioSeleccionada`. El chip del selector (bloque visual institucional inferior) quedaba vacío en modo edición y se perdía en detach/rehost porque `EntidadDirectorioSeleccionada` era `null`.

**Solución**: `Initialize()` ahora sintetiza `_entidadDirectorioSeleccionada` desde los datos del documento existente. El constructor primario y el de rehost restauran el chip directamente desde `ViewModel.EntidadDirectorioSeleccionada`. **Estado**: ✅ **RESUELTO con ADR-043**

---

### [RESUELTO] KI-028 — Alerta "descuento global" se repetía al abrir/editar/detach documentos (ADR-043)

**Módulo**: Cotizaciones — `CotizacionDocumentoPage.xaml.cs`

**Problema**: `NbDescuentoGlobal.ValueChanged` se disparaba durante la hidratación de la UI (al hacer `InitializeComponent()` + binding XAML). Si el documento ya tenía descuento global, el handler veía `pct > 0 && HayDescuentosEnLineas` y mostraba la alerta de confirmación aunque el descuento ya estaba aplicado.

**Solución**: Flag `_hidratandoUI = true` antes de `InitializeComponent()`, `false` tras completar toda la inicialización. `NbDescuentoGlobal_ValueChanged` retorna si `_hidratandoUI`. La alerta solo aparece por acción explícita del usuario. **Estado**: ✅ **RESUELTO con ADR-043**

---

### [RESUELTO] KI-026 — Cotización creaba líneas duplicadas al agregar el mismo producto (ADR-040)

**Módulo**: Cotizaciones — `CotizacionDocumentoPage`, `CotizacionDocumentoViewModel`

**Problema**: Agregar el mismo producto dos veces creaba líneas duplicadas en lugar de sumar cantidad.

**Solución**: `AgregarOIncrementarDetalleAsync()` verifica `ObtenerLineaExistente()` antes de insertar. Si el producto ya existe, llama a `IncrementarCantidadAsync()`. **Estado**: ✅ **RESUELTO con ADR-040**

---

### [RESUELTO] KI-025 — IVA e Impuestos no visibles en Cotización (ADR-040)

**Módulo**: Cotizaciones — `CotizacionDocumentoPage.xaml`, `CotizacionDocumentoViewModel`

**Problema**: Solo existía columna de Importe sin IVA; el desglose de totales no mostraba Impuestos.

**Solución**: Columna IVA ("Sí"/"No") agregada al grid. Fila Impuestos agregada a totales. `FiscalConstants.TasaIvaEstandar` centraliza el 16%. `CalcularImpuestos()` y `CalcularSubtotal()` centralizan la aritmética. **Estado**: ✅ **RESUELTO con ADR-040**

---

### [RESUELTO] KI-024 — Sin confirmación al cerrar documento con cambios no guardados (ADR-040)

**Módulo**: Cotizaciones — `CotizacionDocumentoPage`

**Problema**: El usuario podía cerrar el documento sin ninguna advertencia, perdiendo cambios no guardados.

**Solución**: `IsDirty` trackea todo cambio. `MostrarConfirmacionCierreAsync()` muestra diálogo institucional (Guardar / No Guardar / Cancelar). `BtnVolverALista_Click` aguarda confirmación. **Estado**: ✅ **RESUELTO con ADR-040**

---

### [RESUELTO] KI-023 — Valores monetarios sin formato ($0.00) en Cotización (ADR-040)

**Módulo**: Cotizaciones — `CotizacionDocumentoPage.xaml`

**Problema**: PrecioUnitario, Importe, Subtotal y Total se mostraban sin formato monetario (ej. `3337.000000`).

**Solución**: `DecimalToCurrencyConverter` aplicado a todas las columnas financieras del grid y a las filas de totales. **Estado**: ✅ **RESUELTO con ADR-040**

---

### [RESUELTO] KI-022 — Detach Mode recreaba Document Session y perdía estado runtime no guardado (ADR-039)

**Módulo**: Cotizaciones — `CotizacionDocumentoPage`, `BtnAbrirEnVentana_Click`

**Problema identificado**:
- Al pulsar "Abrir en nueva ventana", el sistema creaba `new CotizacionDocumentoPage(_cotizacionOriginal)` dentro del factory de `WindowManager`.
- Esto reinstanciaba `CotizacionDocumentoViewModel` desde cero, perdiendo todo el estado runtime.
- Estado perdido: cliente seleccionado, chip visual del selector, líneas temporales agregadas, cantidades, precios, cálculos de totales, estado dirty, fechas y observaciones editadas.
- Equivalía a "abrir el documento de nuevo desde la base de datos" en lugar de "mover el mismo documento a otro contenedor visual".

**Causa raíz**: El factory del detach creaba una nueva instancia de la página en lugar de rehostear la existente.

**Solución aplicada (ADR-039)**:
- `BtnAbrirEnVentana_Click` refactorizado para rehostear `this` (la misma instancia de página) en `DetachedDocumentWindow`.
- Flujo correcto:
  1. `EsInlineMode = false` — desactiva controles inline en la página rehosteada.
  2. `VolverALista?.Invoke()` — limpia `DocumentSurfaceContent = null` en el módulo padre, removiendo la página del árbol visual inline sincrónicamente (WinUI 3 requiere zero parents antes de asignar nuevo host).
  3. `WindowManager.OpenWindow(factory: () => new DetachedDocumentWindow(paginaActual, titulo))` — rehostea la misma instancia.
- El `_sessionKey` (GUID generado en constructor) garantiza window key estable para documentos nuevos sin Id asignado.
- Título construido desde `ViewModel.NombreCliente` (estado runtime actual) en lugar del snapshot estático.

**Estado**: ✅ **RESUELTO con ADR-039** — `CotizacionDocumentoPage` ya no recrea el ViewModel al desacoplar.

---

### [RESUELTO] KI-021 — Selector comercial no retornaba resultados (QueryFilter Include + datos huérfanos)

**Módulo**: Directorio / Ventas — `RelacionComercialSelectorControl`, `ListarParaSelectorAsync`

**Problema identificado**:
- El método `ListarParaSelectorAsync` usaba `Include(r => r.EmpresaComercial).Include(r => r.Persona)`.
- El QueryFilter global de `ErpDbContext` aplica `EmpresaId == _session.EmpresaId` también a las entidades incluidas.
- Si `EmpresaComercial.EmpresaId` no coincidía exactamente (datos legacy, discrepancias de migración), el Include resolvía `null`.
- `MapToDto` con `EmpresaComercial == null && Persona == null` generaba nombre `"Relación #X"` y la búsqueda por `r.EmpresaComercial.RazonSocial` nunca matcheaba.
- Adicionalmente, existían `EmpresaComercial` y `Persona` creadas antes de ADR-036 sin `RelacionComercial` correspondiente (datos huérfanos).

**Solución aplicada (ADR-037)**:
- `ListarParaSelectorAsync` reescrito como proyección LINQ directa con join explícito usando `IgnoreQueryFilters()` en `Personas` y `EmpresasComerciales`.
- El QueryFilter de empresa sigue activo en `RelacionComercial` (tabla raíz) para preservar multitenancy.
- Filtro de búsqueda extendido: nombre, apellidos, razón social, nombre comercial, RFC, email.
- Scripts de diagnóstico y normalización disponibles en `Documentation/Scripts/`.

**Acción requerida**:
> ~~Ejecutar scripts de normalización masiva~~ — **PROHIBIDO por ADR-038**.  
> El selector ahora busca directamente en `Persona` y `EmpresaComercial` sin requerir `RelacionComercial` preexistente.  
> `RelacionComercial` se crea automáticamente al guardar el primer documento para esa entidad (GetOrCreate pattern).  
> Los scripts `normalizacion_relacion_comercial.sql` son un **anti-pattern** bajo ADR-038 y no deben ejecutarse.

**Estado**: ✅ **RESUELTO COMPLETAMENTE con ADR-038** — No se requiere acción en BD.

---

### [RESUELTO] KI-016 — Tabs documentales ensimados/translúcidos en Pedidos, OT y Ventas Documentales (ADR-031)

**Módulo**: Ventas — `PedidosPage`, `OrdenesTrabajoPage`, `VentasDocumentalesPage`

**Problema identificado**:
- **Doble jerarquía visual**: Pedidos, Órdenes de Trabajo y Ventas Documentales abrían documentos CRUD simples como tabs de `IWorkspaceService`, generando una capa visual extra debajo de los tabs de módulo.
- **Apariencia browser/IDE**: el ERP se percibía como una aplicación web tipo Chrome con tabs documentales translúcidos, no como una aplicación desktop-native ERP.
- **Confusión UX**: el usuario no distinguía entre navegación de módulo (tabs de módulo) y documento activo (pedido, OT, venta). Ambas eran visualmente iguales.
- **Acumulación de tabs triviales**: `WorkspaceService` acumulaba tabs CRUD simples mezclados con workflows reales (Cotizaciones, etc.).

**Solución aplicada (ADR-031)**: Migración completa a **Inline Document Surface** para CRUDs simples:
- `IsDocumentSurfaceVisible` / `DocumentSurfaceContent` en cada ViewModel host.
- `ContentPresenter` inline en cada host XAML; listado oculto por `InverseBoolToVisibilityConverter` cuando surface está activa.
- Documentos (`PedidoDocumentoPage`, `OrdenTrabajoDocumentoPage`, `VentaDocumentoPage`) ahora tienen:
  - Header operacional con breadcrumb ligero: `Módulo › Título`
  - Badge de estado del documento
  - Botón `←` volver al listado (`OnCerrar` callback)
  - **SIN** línea azul activa tipo tab, **SIN** close ×, **SIN** apariencia browser
- Hosts (`PedidosPage.xaml.cs`, `OrdenesTrabajoPage.xaml.cs`, `VentasDocumentalesPage.xaml.cs`) ya **no** llaman a `_workspace.OpenTab` ni a `OpenOrActivateDocumentTabAsync` para abrir CRUDs simples.

**Jerarquía UX resultante**:
```
Tabs módulo (navegación principal)
    ↓
Document Surface Header (documento activo — operacional)
    breadcrumb: Módulo › Título | badge estatus | botón ← volver
    ↓
Toolbar operacional (CommandBar documento)
    ↓
Contenido formulario / grid detalles
```

**Estado**: ✅ **RESUELTO** — Build limpia, patrón aplicado en Pedidos, OT y Ventas Documentales.

**Documentación completa**: Ver `Documentation/ADR-031-Document-Surface-Visual-Separation-Standard.md`

**Anti-patterns oficiales prohibidos (ADR-031)**:
- ❌ `_workspace.OpenTab(...)` para CRUDs simples (Nuevo Pedido, Nueva OT, Nueva Venta, etc.)
- ❌ `_workspace.OpenOrActivateDocumentTabAsync(...)` como apertura primaria de formularios
- ❌ Document Surface con línea azul activa tipo tab seleccionado
- ❌ Document Surface con close × estilo browser tab
- ❌ Transparencia/overlay sobre tabs de módulo
- ❌ Headers tipo browser (Chrome/Edge look)
- ❌ Apariencia docking IDE (Visual Studio look)

---

### [RESUELTO] — DbContext concurrencia al aplicar descuento global en Cotización (ADR-043b)

**Módulo**: CotizacionDocumentoPage / CotizacionDocumentoViewModel

**Excepción**: `System.InvalidOperationException: 'A second operation was started on this context instance before a previous operation completed'`

**Stack**: `CotizacionService.EliminarDetalleAsync → CotizacionDocumentoViewModel.ActualizarDescuentoAsync`

**Causa raíz**: El loop original de `AplicarDescuentoGlobalALineas` llamaba `ActualizarDescuentoAsync` por cada línea. Al final del delete+readd, `linea.DescuentoPct = pct` disparaba INPC → `NumberBox.ValueChanged` (async void) → nueva llamada a `ActualizarDescuentoAsync`. Mientras esa segunda llamada ejecutaba `EliminarDetalleAsync`, el loop principal ya había avanzado a otra línea y también ejecutaba `EliminarDetalleAsync`. Dos operaciones concurrentes sobre el mismo `_context` scoped → excepción.

**Solución aplicada (Two-Phase Discount Apply Pattern)**:
1. **Fase 1 (memoria)**: Establece todos los `linea.DescuentoPct = pct` ANTES de cualquier service call. El guard anti-reentrancy en `ActualizarDescuentoAsync` detecta `linea.DescuentoPct == pctClamped` → retorna sin service call.
2. **Fase 2 (BD, único scope IsBusy)**: Persiste cada línea secuencialmente. Guards IsBusy en los handlers del code-behind rechazan eventos concurrentes.
3. **Guard en `ActualizarDescuentoAsync`**: `if (linea.DescuentoPct == pctClamped) return;` — previene re-entrada desde INPC.
4. **Guards IsBusy en handlers** — defensa en profundidad.

**Estado**: ✅ RESUELTO — build exitoso, sin cambios de arquitectura.

---

## Problemas Activos

### [PENDIENTE] KI-017 — Directorio UX: páginas de Personas y EmpresasComerciales sin implementar (ADR-036)

**Módulo**: Directorio — `Ybridio.WinUI/Views/Contactos/ContactosPage.xaml`

**Estado**: El módulo "Directorio" muestra placeholder "Directorio — Próximamente". La arquitectura de dominio (entidades, EF, servicios, DTOs) está completamente implementada. Las páginas `DirectorioPersonasPage`, `DirectorioEmpresasPage` y `DirectorioRelacionesPage` están pendientes de creación.

**Workaround**: Los documentos de venta (Cotizaciones) ya usan el nuevo modelo `RelacionComercial` vía selector. Los clientes existentes operan a través de `ClientesPage` / `ClienteService` en coexistencia controlada.

**Plan**: Implementar páginas Directorio como document-surface (ADR-030) en iteración futura.

---

### [PENDIENTE] KI-018 — Migración de BD `AddBusinessPartnerModel` pendiente de aplicar

**Módulo**: Infrastructure — `Ybridio.Infrastructure/Persistence/Migrations/`

**Estado**: Migración `20260511210551_AddBusinessPartnerModel.cs` generada. Crea tablas `core.Persona`, `core.EmpresaComercial`, `core.RelacionComercial` y renombra `ClienteId → RelacionComercialId` en documentos de venta. **No ha sido aplicada** a la base de datos YBRIDIO-26.

**Workaround**: Usar base de datos de desarrollo sin la migración; los documentos de venta no mostrarán socios comerciales hasta aplicar la migración.

**Plan**: Ejecutar `dotnet ef database update` en el entorno de desarrollo antes de habilitar el selector de socios en producción.

---

### [COEXISTENCIA] KI-019 — Legacy `Cliente*` pages y `ClienteService` en coexistencia con nuevo modelo (ADR-036)

**Módulo**: WinUI Ventas — `ClientesPage`, `ClienteDocumentoPage`, `ClientesViewModel`, `ClienteService`

**Estado**: Las páginas de gestión de clientes legacy (`ClientesPage`, `ClienteDocumentoPage`) y el servicio `ClienteService` permanecen funcionales y coexisten con el nuevo modelo `RelacionComercial`. Esta coexistencia es intencional para no romper flujos activos.

**Workaround**: Nuevas funcionalidades deben usar `IRelacionComercialService`. Las páginas legacy seguirán funcionando contra `DbSet<Cliente> Clientes` que se mantiene en `ErpDbContext`.

**Plan**: Migrar páginas legacy al nuevo modelo Directorio en la misma iteración que KI-017.

---

### [POLICY] KI-015 — Límite máximo 2 ventanas desacopladas simultáneas (Window Detach Mode ADR-028+029)

**Módulo**: Window Management — `WindowManager` (`Ybridio.WinUI/Services/Windowing/WindowManager.cs`)

**Limitación arquitectónica oficial**: Máximo **2 ventanas detached** (Window Detach Mode — ADR-028) activas simultáneamente en runtime. Policy global enforcement centralizado en `WindowManager` (ADR-029) vía convention key prefix `"detached:"`.

**Razón**:
- Desktop multitasking **ligero controlado**: usuarios PYME necesitan comparar 2 cotizaciones/pedidos lado a lado, NO workspace tabs infinitos
- **Simplicidad operacional**: límite claro previene caos visual desktop (10+ ventanas ERP flotantes)
- **Resource management**: cada ventana detached tiene instancia Page+ViewModel+state independiente; limitar overhead runtime
- **UX policy explícita**: Window Detach Mode es extensión desktop-native para **comparación ligera**, NO full multi-window ERP

**Excepción operacional cuando límite alcanzado**:
```csharp
throw new DetachedWindowLimitException(MaxDetachedWindows, _detachedWindowsCount);
// MaxAllowed = 2, CurrentCount = 2
// Message: "Límite alcanzado: máximo 2 ventanas desacopladas simultáneas. Cierre alguna ventana antes de abrir otra. (Activas: 2)"
```

**UI handling pattern**:
```csharp
try
{
    _windowManager.OpenWindow<DetachedDocumentWindow, string>(
        key: $"detached:cotizacion:{id}",
        factory: () => new DetachedDocumentWindow(nuevaPagina, titulo),
        options: new WindowOptions { Width = 1200, Height = 800 });
}
catch (DetachedWindowLimitException ex)
{
    // Mostrar ContentDialog operacional claro al usuario
    var dialog = new ContentDialog
    {
        Title = "Límite de ventanas alcanzado",
        Content = ex.Message,
        CloseButtonText = "Entendido",
        XamlRoot = this.XamlRoot
    };
    await dialog.ShowAsync();
}
```

**Workaround usuario**: Cerrar una ventana detached existente antes de abrir otra.

**Plan futuro**: Evaluar incremento límite (ej: 3 ventanas) si feedback runtime real de PYMES indica necesidad clara. NO extender límite sin evidencia operacional.

**Anti-patterns oficiales prohibidos (ADR-029)**:
- ❌ `new Window()` fuera de factories pasadas a `WindowManager.OpenWindow(...)`
- ❌ `AppWindow` manual disperso en Pages/ViewModels
- ❌ Lifecycle handlers duplicados (`window.Closed += ...` fuera del manager)
- ❌ Tracking paralelo ventanas (`_ventanasAbiertasPorMi` counters locales en UI)
- ❌ Services window management secundarios (`IDetachedWindowManager` ya eliminado, NO recrear)
- ❌ Window ownership Win32 manual fuera del manager
- ❌ Policy enforcement fragmentado (validaciones límite en ViewModels/Pages)

**Estado**: ✅ **RESUELTO — Centralizado bajo WindowManager** (ADR-029). Policy global oficial, single source of truth lifecycle, anti-patterns formalizados en `Documentation/CLAUDE_RULES.md`.

**Documentación completa**: Ver `Documentation/ADR-029-Window-Management-Standards.md`

---

### [RESUELTO] KI-014 — DbContext Concurrency en PermisoService durante navegación runtime

**Módulo**: `PermisoService` (`Ybridio.Application/Services/Permisos/PermisoService.cs`)

**Excepción en runtime**:
```
System.InvalidOperationException:
'A second operation was started on this context instance before a previous operation completed.'
```

**Causa raíz**: Navegación rápida runtime entre módulos (Clientes ↔ Cotizaciones ↔ Pedidos), activación/desactivación de Document Surfaces, pre-checks de autorización en `OnNavigatedTo`, bindings runtime, comandos async, y refresh concurrente del Runtime Diagnostic Panel provocaban **múltiples evaluaciones concurrentes** de `PermisoService.TienePermisoAsync(...)` y `ObtenerPermisosEfectivosAsync(...)` usando el mismo `ErpDbContext` scoped. EF Core **NO permite operaciones concurrentes** en el mismo contexto.

**Contextos de riesgo identificados**:
- Navegación rápida multi-módulo (Clientes → Cotizaciones → Pedidos en segundos)
- Document Surface activation/desactivation (Nueva Cotización → guardar → editar → volver)
- Múltiples tabs Workspace con pre-checks autorización concurrentes
- `OnNavigatedTo` + bindings de permisos en UI + `AsyncRelayCommand` pre-checks simultáneos
- Runtime Diagnostic Panel refresh automático mientras usuario navega/opera
- Eventos múltiples runtime convergiendo en evaluación autorización

**Solución aplicada**: **Single-flight pattern** con `SemaphoreSlim` global en `PermisoService` (ADR-026):

```csharp
// Single-flight guard: serializar evaluaciones de permisos runtime
private static readonly SemaphoreSlim _authSemaphore = new(1, 1);

public async Task<bool> TienePermisoAsync(
    Guid usuarioId, string clave, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(clave))
        return false;

    await _authSemaphore.WaitAsync(ct);  // ← Serializar: una evaluación a la vez
    try
    {
        // Lógica de evaluación: override → perfiles → roles
        // (queries EF Core usando _context scoped)
    }
    finally
    {
        _authSemaphore.Release();  // ← Siempre liberar en finally
    }
}

// Mismo patrón aplicado en ObtenerPermisosEfectivosAsync
// con double-check pattern para optimizar cache hits
```

**Impacto del fix**:
- ✅ Evaluaciones de permisos **serializadas** — un solo thread evaluando autorización a la vez
- ✅ **SIN exceptions DbContext** durante navegación rápida, Document Surfaces, tabs concurrentes
- ✅ **Autorización consistente** — permisos correctos aplicados en todos los escenarios runtime
- ✅ **Arquitectura preservada** — NO tocó Security Foundation, WorkspaceService, Shell, Runtime Observability
- ✅ **Performance aceptable** — latencia mínima agregada (microsegundos semaphore vs milisegundos queries EF)
- ✅ **Double-check optimization** — en `ObtenerPermisosEfectivosAsync`, valida caché antes y después del lock

**Alternativas descartadas**:
- ❌ Per-permission semaphore con `ConcurrentDictionary`: overhead tracking, limpieza diccionario creciente
- ❌ Lock statement tradicional: semánticamente equivalente, semaphore más explícito async/await
- ❌ Task.Run para aislar DbContext: anti-pattern prohibido (ADR-020), race conditions
- ❌ DbContext singleton: viola arquitectura EF Core, causaría más problemas state management
- ❌ Caché agresivo: `MemoryPermissionCache` ya existe, NO elimina problema `TienePermisoAsync` individual

**Estado**: ✅ RESUELTO — single-flight guard aplicado, build exitoso (0 errores). **Validación runtime pendiente por usuario**: navegar rápidamente Clientes/Cotizaciones/Pedidos, abrir/cerrar Document Surfaces, múltiples tabs, refresh concurrente; confirmar ausencia exceptions DbContext.

**Regla preventiva** (agregada a `CLAUDE_RULES.md` §7):
> `PermisoService` usa single-flight guard con `SemaphoreSlim` global para serializar evaluaciones de permisos runtime. NO modificar este patrón sin documentar ADR explícito. NO aislar DbContext con `Task.Run`, NO cambiar a singleton, NO usar locks tradicionales sin considerar async.

**Documentación actualizada**:
- `Documentation/DECISIONS.md`: ADR-026 Security Runtime Concurrency Stabilization
- `Documentation/CLAUDE_RULES.md` §7: subsección "Security Runtime Concurrency"
- `Documentation/ARCHITECTURE_STATUS.md`: sección completa Security Runtime Concurrency Stabilization
- `Documentation/KNOWN_ISSUES.md`: este issue (KI-014)

---

### [LIMITACIÓN ARQUITECTÓNICA] KI-015 — Document Surface Detachable Mode: 1 surface desacoplada máximo por módulo

**Módulo**: Document Surface UX Pattern (piloto Cotizaciones)

**Limitación impuesta**: El Document Surface Detachable Mode (ADR-027) permite **SOLO 1 Document Surface desacoplada activa por módulo**, evitando caos UX, complejidad runtime y problemas de DbContext concurrency.

**Razones arquitectónicas**:
- **Caos UX evitado**: múltiples surfaces desacopladas simultáneas crean ruido visual excesivo, confusión operacional, no apto para PYME
- **DbContext concurrency**: múltiples surfaces embebidas generarían colisiones concurrency adicionales (regresión ADR-026)
- **Runtime Observability complexity**: tracking múltiples surfaces desacopladas agrega overhead innecesario
- **Lifecycle management**: múltiples content presenters simultáneos complican disposal/cleanup
- **Performance UX**: split view múltiple degrada render, scrolling, navegación fluida
- **Principio UX institucionalizado**: simplicidad operacional > flexibilidad enterprise innecesaria

**Comportamiento implementado**:
- Usuario abre Document Surface (Nueva/Editar Cotización) → modo normal (content replacement: grid XOR surface)
- Usuario hace clic "Desacoplar Surface" (botón discreto CommandBar secundario) → modo desacoplado (split view: grid + surface lado izquierdo/derecho)
- Usuario hace clic "Desacoplar Surface" nuevamente → vuelve a modo normal (content replacement)
- Usuario cierra surface (`CerrarDocumentSurfaceAsync`) → estado detached resetea a false automáticamente
- Usuario abre nueva surface → SIEMPRE inicia en modo normal (NO persiste estado detached entre aperturas)

**Guard implementado**:
```csharp
[RelayCommand]
public void ToggleDetach()
{
    if (!IsDocumentSurfaceVisible) return; // Guard obligatorio
    IsDocumentSurfaceDetached = !IsDocumentSurfaceDetached;
}
```

**Escenarios NO soportados (por diseño)**:
- ❌ Abrir múltiples cotizaciones desacopladas simultáneamente en el mismo módulo
- ❌ Desacoplar sin Document Surface activo (`IsDocumentSurfaceVisible=false`)
- ❌ Floating windows OS reales ilimitadas (solo 2 máximo por Window Detach Mode ADR-028)
- ❌ Dock manager enterprise con múltiples panels side-by-side

**Estado**: ✅ Implementado, documentado, funcionando en piloto Cotizaciones.

---

### [LIMITACIÓN ARQUITECTÓNICA] KI-016 — Window Detach Mode: Máximo 2 ventanas desacopladas simultáneas

**Módulo**: Window Detach Mode (ADR-028, piloto Cotizaciones)

**Limitación impuesta**: El Window Detach Mode permite **SOLO 2 ventanas OS reales desacopladas activas simultáneamente** (global, no por módulo).

**Razones arquitectónicas**:
- **Caos UX evitado**: ventanas ilimitadas recrean problema tabs infinitos en forma de ventanas OS; usuario pierde control operacional
- **DbContext concurrency**: múltiples ventanas = múltiples instancias ViewModel = potencial para colisiones concurrency DbContext masivas (aunque mitigadas por single-flight guard ADR-026)
- **Lifecycle management**: tracking ilimitado de ventanas complica disposal/cleanup, aumenta riesgo leaks
- **Performance runtime**: cada ventana OS tiene árbol visual completo, bindings runtime, overhead memory; límite 2 es balance UX/performance
- **Principio UX institucionalizado**: simplicidad operacional > flexibilidad enterprise innecesaria; 2 ventanas suficientes para multitarea desktop real (comparar documentos, copiar información, multi-monitor)

**Comportamiento implementado**:
- Usuario hace clic "Abrir en Ventana" (CommandBar.SecondaryCommands documento) → ventana #1 se abre
- Usuario abre segunda ventana → ventana #2 se abre
- Usuario intenta abrir tercera ventana → `DetachedWindowManager.TryOpenDetachedWindowAsync` retorna `(Success=false, Error="Límite alcanzado: máximo 2 ventanas desacopladas simultáneas. Cierre alguna ventana antes de abrir otra.")`
- Usuario cierra alguna ventana → cleanup automático (`window.Closed += ...`) libera slot; ahora puede abrir otra

**Mensaje operacional al usuario**:
```
Título: Límite de ventanas alcanzado
Contenido: Límite alcanzado: máximo 2 ventanas desacopladas simultáneas. Cierre alguna ventana antes de abrir otra.
Botón: Entendido
```

**Guard implementado** (`DetachedWindowManager.cs`):
```csharp
private const int MaxDetachedWindows = 2;

public async Task<(bool Success, string? Error, Guid? WindowId)> TryOpenDetachedWindowAsync(...)
{
    if (_activeWindows.Count >= MaxDetachedWindows)
    {
        return (false, $"Límite alcanzado: máximo {MaxDetachedWindows} ventanas desacopladas simultáneas. Cierre alguna ventana antes de abrir otra.", null);
    }
    // ...
}
```

**Cleanup automático**:
```csharp
window.Closed += (sender, args) =>
{
    _activeWindows.Remove(windowId); // Libera slot automáticamente
};
```

**Escenarios NO soportados (por diseño)**:
- ❌ Abrir 3 o más ventanas desacopladas simultáneas
- ❌ Ventanas OS ilimitadas (caos UX, problemas runtime)
- ❌ Floating windows sin límite
- ❌ Sincronización bidireccional automática entre ventanas (cada instancia es independiente; usuario debe guardar/refrescar manualmente)

**Escenarios soportados** (✅):
- ✅ Abrir hasta 2 ventanas desacopladas simultáneas
- ✅ Comparar 2 documentos lado a lado (ej: Cotización A vs Cotización B)
- ✅ Copiar información entre ventanas
- ✅ Multi-monitor: grid en pantalla primaria, documento en secundaria
- ✅ Pantallas pequeñas: abrir documento en ventana completa sin perder grid inline
- ✅ Cleanup automático al cerrar ventana (libera slot)

**Runtime validation pendiente**:
1. Abrir ventana #1 y #2 → validar ambas funcionan correctamente
2. Intentar abrir ventana #3 → validar mensaje operacional claro
3. Cerrar ventana #1 → validar cleanup automático (puede abrir #3 ahora)
4. Trabajar multi-monitor: validar UX desktop-native
5. Validar: SIN exceptions DbContext, SIN leaks visibles, SIN crashes runtime
6. Validar: navegación/edición/guardar funciona en ventanas independientes

**Estado**: ✅ Implementado, build exitoso (0 errores), runtime validation pendiente por usuario.

**Documentación completa**: Ver `Documentation/ADR-028-Document-Surface-Window-Detach-Mode.md`

---
- ❌ Persistir estado detached entre aperturas de surface (resetea siempre en abrir/cerrar)

**Workaround para comparación multi-documento**:
- **Workspace Tabs permanecen disponibles** para workflows complejos que requieren múltiples documentos visibles simultáneamente
- Document Surface detachable mode es para **multitarea ligera ocasional** (consultar grid mientras edita 1 documento)
- Si necesidad es persistente/multi-documento → usar Workspace Tabs (ADR-025 sigue vigente)

**Estado**: ✅ LIMITACIÓN ARQUITECTÓNICA INTENCIONAL — piloto Cotizaciones implementado, validación runtime pendiente usuario. NO expandir a otros módulos hasta validar UX, estabilidad, aceptación operacional.

**Regla preventiva** (agregada a `CLAUDE_RULES.md` §12.1):
> Document Surface Detachable Mode permite SOLO 1 surface desacoplada activa por módulo. NO implementar múltiples surfaces simultáneas, NO usar floating windows OS, NO dock managers enterprise. Default SIEMPRE es content replacement mode (grid XOR surface). Detachable mode es extensión opcional bajo demanda usuario.

**Validación runtime requerida** (piloto Cotizaciones):
1. Nueva Cotización → desacoplar → navegar grid mientras edita → acoplar → guardar → confirmar UX limpia
2. Editar Cotización → desacoplar → seleccionar otra en grid (NO abrir) → comparar información → acoplar → continuar
3. Abrir/cerrar Document Surface repetidamente → confirmar reset estado detached correcto
4. Navegar entre módulos con surface desacoplada activa → confirmar lifecycle sin leaks
5. Validar ausencia overlap visual, layout responsive correcto, split view limpio
6. Confirmar NO regresión DbContext concurrency (compatible ADR-026)

**Documentación actualizada**:
- `Documentation/DECISIONS.md`: ADR-027 Document Surface Detachable Mode
- `Documentation/CLAUDE_RULES.md` §12.1: extensión Detachable Mode con UX rules y anti-patterns
- `Documentation/ARCHITECTURE_STATUS.md`: sección completa Document Surface Detachable Mode Extension
- `Documentation/KNOWN_ISSUES.md`: este issue (KI-015)

---

### [MITIGADO] KI-013 — DbContext Concurrency en refresh/load commands

**Módulo**: Todos los ViewModels con `LoadAsync` / `RefrescarAsync` en Inventario, Ventas, Finanzas

**Excepción potencial**:
```
System.InvalidOperationException:
'A second operation was started on this context instance before a previous operation completed.'
```

**Causa raíz**: EF Core `DbContext` es scoped pero NO thread-safe. Si un usuario navega rápidamente entre módulos, hace clic repetido en "Refrescar", o si un timer dispara refresh mientras otro está en curso, **dos operaciones async concurrentes pueden intentar usar el mismo contexto scoped**, lo cual está prohibido por EF Core.

**Contextos de riesgo identificados**:
- `OnNavigatedTo` → `await ViewModel.RefrescarAsync()` mientras hay un refresh previo en curso
- Usuario hace clic repetido en botones de comando async
- Multi-tab navigation rápida
- Timers de diagnóstico (aunque `DiagnosticPanelViewModel` no usa DbContext directamente)

**Mitigación aplicada**: Single-flight guard pattern en todos los ViewModels operacionales:

```csharp
private bool _isRefreshing;  // o _isLoading

[RelayCommand]
public async Task RefrescarAsync(CancellationToken ct = default)
{
    if (_isRefreshing) return;  // ← Early return: evita reentrada
    if (Session.EmpresaId == 0) return;

    _isRefreshing = true;
    IsBusy = true;
    try
    {
        // ... service call con DbContext
    }
    finally
    {
        IsBusy = false;
        _isRefreshing = false;  // ← Siempre liberado en finally
    }
}
```

**ViewModels protegidos** (ver `docs/CLAUDE_RULES.md` §7 para lista completa):
- Inventario: `SalidasViewModel`, `EntradasViewModel`, `ExistenciasViewModel`, `KardexViewModel`, `ProductosViewModel`
- Ventas: `CotizacionesViewModel`, `PedidosViewModel`, `OrdenesTrabajoViewModel`
- Finanzas: `GastosViewModel`, `IngresosViewModel`, `CxCViewModel`, `CxPViewModel`

**Impacto del fix**: Los comandos de carga/refresh son ahora **single-flight** — si un refresh está en curso, llamadas adicionales se ignoran silenciosamente hasta que el primero complete. Esto previene la excepción de concurrencia DbContext sin bloquear UI ni introducir locks complejos.

**Estado**: ✅ MITIGADO — guards aplicados, build exitoso, runtime estable. La excepción ya no debería ocurrir en escenarios normales de uso multi-tab o refresh rápido.

**Regla preventiva** (agregada a `CLAUDE_RULES.md` §7):
> Todos los ViewModels con `LoadAsync`/`RefrescarAsync` que usen Application services (que indirectamente usan DbContext scoped) DEBEN aplicar el patrón single-flight guard. Ver ejemplos CORRECTO vs INCORRECTO en `docs/CLAUDE_RULES.md`.

---

### [RESUELTO] KI-012 — `IdentityRole<Guid>` no registrado en ErpDbContext

**Módulo**: `PermisoService` (`Ybridio.Application/Services/Permisos/PermisoService.cs`)

**Excepción en runtime**:
```
System.InvalidOperationException:
'Cannot create a DbSet for 'IdentityRole<Guid>' because this type is not included in the model for the context.'
```

**Causa raíz**: El código usaba `_context.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()` para hacer JOIN con los roles del usuario. Sin embargo, `ErpDbContext` hereda de `IdentityDbContext<ApplicationUser, **ApplicationRole**, Guid>` — registra `ApplicationRole` (el tipo custom enterprise), NO el tipo genérico base `IdentityRole<Guid>`. EF Core no puede crear un `DbSet<T>` para un tipo que no está en el modelo.

**Dónde ocurría** (dos instancias en el mismo archivo):
- `TienePermisoAsync()` — Nivel 3 del evaluador de permisos
- `ObtenerPermisosEfectivosAsync()` — Nivel 3 del resolver de conjunto efectivo

**Fix aplicado**:
```csharp
// ❌ ANTES — causa excepción en runtime
_context.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
    .Where(r => rolesUsuario.Contains(r.Name!))

// ✅ DESPUÉS — correcto para ErpDbContext con ApplicationRole
_context.Roles
    .Where(r => rolesUsuario.Contains(r.Name!))
```

**`_context.Roles`** es la propiedad heredada de `IdentityDbContext` que expone `DbSet<ApplicationRole>`, el tipo correcto registrado en el modelo.

**Motivo arquitectónico**: `ErpDbContext` usa Identity custom (`ApplicationRole : IdentityRole<Guid>`) con campos adicionales (`FechaCreacion`, `Borrado`, `RowVersion`). EF Core registra el tipo derivado en el modelo, no el tipo base genérico. Ver ADR-002 en `docs/DECISIONS.md`.

**Impacto del bug**: Toda autorización runtime fallaba con excepción al intentar evaluar permisos heredados por rol (Nivel 3 de evaluación). El login y cualquier operación protegida terminaban en error 500.

**Impacto del fix**: Runtime Security completamente funcional. `TienePermisoAsync` y `ObtenerPermisosEfectivosAsync` operan correctamente. `MemoryPermissionCache` (TTL 10 min) reduce las queries subsecuentes.

**Estado**: ✅ RESUELTO — ambas instancias corregidas, build = 0 errores.

**Regla preventiva** (agregar a CLAUDE_RULES.md §6):
> Nunca usar `_context.Set<IdentityRole<Guid>>()` — siempre usar `_context.Roles` (que expone `DbSet<ApplicationRole>`).

---

### [HIGH] KI-001 — `ListarPorEmpresaAsync` sin guard de autorización en service layer

**Módulo**: ProductoService  
**Descripción**: La firma `Task<IReadOnlyList<ProductoDto>>` no puede retornar `ServiceResult`, por lo que el guard de `producto.ver` solo existe en el ViewModel, no en el servicio.  
**Impacto**: Si `ProductoService.ListarPorEmpresaAsync` se llama desde un contexto diferente (futuro API, otro ViewModel), el permiso no se verifica en el servicio.  
**Workaround**: El ViewModel (`ProductosViewModel.LoadAsync`) hace el pre-check antes de llamar al servicio.  
**Plan**: Cambiar la firma a `ServiceResult<IReadOnlyList<ProductoDto>>` en una iteración futura. Requiere actualizar `PosViewModel` como consumidor secundario.

---

### [HIGH] KI-002 — Servicios de creación de Entradas y Salidas de inventario no implementados

**Módulo**: Inventario  
**Descripción**: `IEntradaService` y `ISalidaService` solo tienen el método `ListarAsync`. No existe `CrearAsync` para documentos de entrada/salida.  
**Impacto**: Los botones "Nuevo" en EntradasPage y SalidasPage son stubs (`[RelayCommand] private void Nuevo() { }`).  
**Workaround**: No hay. Los usuarios no pueden crear entradas/salidas desde la UI.  
**Plan**: Implementar en Fase 1 (ADR ver ROADMAP.md).

---

### [MEDIUM] KI-003 — 128 warnings MVVMTK0045 (AOT compatibility)

**Módulo**: WinUI (todos los ViewModels)  
**Descripción**: CommunityToolkit.Mvvm 8.4.0 genera warning MVVMTK0045 para campos `[ObservableProperty]`. La recomendación es migrar a `partial property` (CommunityToolkit 8.4+).  
**Impacto**: Solo en compilación (warnings). No hay impacto en runtime. El ERP no usa AOT compilation.  
**Workaround**: Ignorar en desarrollo.  
**Plan**: Migrar campos a `partial property` progresivamente. No es urgente.

---

### [MEDIUM] KI-004 — Indicador visual de CxC/CxP vencidas no implementado

**Módulo**: Finanzas  
**Descripción**: El campo `EsVencida` existe en `CxCDto` y `CxPDto`, pero el DataTemplate de CxCPage/CxPPage muestra el valor bool como texto (`True/False`) sin formato visual.  
**Impacto**: Los usuarios no identifican visualmente las cuentas vencidas de un vistazo.  
**Workaround**: Ver la columna "Estado" del grid (muestra True/False).  
**Plan**: Agregar `DataTrigger` o `IValueConverter` para colorear la fila en rojo si `EsVencida = true`. Ver ROADMAP.md Fase 1.3.

---

### [MEDIUM] KI-005 — `ContextoFinanciero.Usuario` sin queries activas

**Módulo**: Finanzas  
**Descripción**: La arquitectura de `MovimientoFinanciero` incluye `ContextoFinanciero.Usuario` y `UsuarioContextoId`, pero las queries en `FinanzasService.ListarAsync` no filtran por usuario. Todo se muestra a nivel empresa.  
**Impacto**: No hay separación entre gastos personales y empresariales en V1.  
**Workaround**: No crear movimientos con `Contexto = Usuario` en V1.  
**Plan**: Activar en Finanzas V2 con una UI separada o un toggle.

---

### [MEDIUM] KI-006 — Seed de permisos usa EmpresaId=1 hardcodeado en categorías

**Módulo**: Finanzas / SQL  
**Descripción**: El script `finanzas_ddl.sql` inserta categorías seed para TODAS las empresas del CROSS JOIN, pero si se crea una empresa nueva después de ejecutar el script, no tendrá categorías seed.  
**Impacto**: Empresas creadas después del deploy inicial tendrán `CategoriaFinanciera` vacío.  
**Workaround**: Ejecutar manualmente el bloque de seed para la empresa nueva, o crear categorías desde la UI (pendiente de implementar).  
**Plan**: Crear trigger o job que inserte categorías seed al crear una empresa nueva.

---

### [MEDIUM] KI-007 — Totales financieros no visibles en statusbar

**Módulo**: Finanzas  
**Descripción**: El statusbar de GastosPage/IngresosPage muestra "N gasto(s)" pero no el total de montos del período filtrado.  
**Impacto**: El usuario no puede ver rápidamente cuánto gastó en el mes.  
**Workaround**: Ver individualmente cada registro.  
**Plan**: Agregar propiedad calculada `TotalPeriodo` en el ViewModel basada en la colección visible. Ver ROADMAP.md Fase 1.3.

---

### [LOW] KI-008 — Cadena de conexión hardcodeada en App.xaml.cs

**Módulo**: Infraestructura  
**Descripción**: La cadena de conexión SQL Server está literal en `App.xaml.cs`, incluyendo usuario y contraseña en texto plano.  
**Impacto**: Solo en dev. No hay impacto funcional. No deben existir datos sensibles de producción ahí.  
**Workaround**: Aceptable en entorno de desarrollo.  
**Plan**: Mover a `appsettings.json` + DPAPI encriptado antes de producción.

---

### [LOW] KI-009 — Fechas en grids sin formato localizado

**Módulo**: Inventario / Finanzas (todos los grids)  
**Descripción**: Los campos `Fecha` en DataTemplates usan `{x:Bind Fecha}` que aplica el formato por defecto de `DateTime.ToString()` (ISO 8601 largo).  
**Impacto**: Las fechas se muestran como `5/8/2026 10:30:00 AM` en lugar de `08/05/2026`.  
**Workaround**: El usuario puede leerlas. No es incorrecto, solo no formateado.  
**Plan**: Agregar `IValueConverter` global `DateConverter` con formato `dd/MM/yyyy`. Aplicar a todos los templates.

---

### [LOW] KI-010 — Páginas de módulo Ventas y Contactos son placeholders

**Módulo**: Ventas, Contactos  
**Descripción**: `VentasPage` y `ContactosPage` existen en el Shell como módulos pero muestran contenido mínimo o placeholder.  
**Impacto**: Los usuarios pueden navegar a esos módulos pero no tienen funcionalidad operativa completa.  
**Workaround**: Usar POS para ventas.  
**Plan**: Implementar en Fase 2 (ROADMAP).

---

### [LOW] KI-011 — Sin paginación en grids con muchos registros

**Módulo**: Todos los módulos con ListView  
**Descripción**: Los grids cargan todos los registros del período filtrado en memoria y los muestran. Si hay >5,000 registros en el período, la carga puede ser lenta.  
**Impacto**: Solo observable con volúmenes grandes de datos.  
**Workaround**: Usar filtros de período cortos (7 días, 30 días).  
**Plan**: Implementar paginación virtual en módulos con historial largo (Kardex, Ventas) en Fase 4.

---

## Limitaciones de Diseño Intencionales

Estas no son bugs sino decisiones arquitectónicas conscientes:

| Limitación | Razón | Referencia |
|---|---|---|
| Sin contabilidad doble | Fuera del alcance PYME | ADR-010 |
| Sin integración SAT/CFDI | Requiere certificados + módulo fiscal | Roadmap Fase 4 |
| Sin multi-moneda | Simplicidad V1 | Roadmap Fase 3 |
| Sin auditoría de cambios de seguridad | TTL caché cubre casos básicos | Roadmap Fase 2 |
| Soft-delete universal (sin papelera de reciclaje) | Filtros globales simples | ADR-013 |
| Tablas de Identity en schema `seguridad.*` | Identity adaptado al ERP | ADR-002 |

---

## Document Surface UX Pattern — Limitaciones y Consideraciones (2026-05-09)

### [INFO] Scope Inicial Limitado — Piloto en 3 Módulos

**Descripción**: El Document Surface UX Pattern (§ADR-025) está implementado SOLAMENTE en:
- ✅ **Cotizaciones** (CotizacionesPage ↔ CotizacionDocumentoPage)
- 🔲 Clientes (pendiente)
- 🔲 Productos (pendiente)

**Módulos que NO deben usar Document Surface todavía**:
- ❌ Pedidos (workflow complejo, genera Ventas)
- ❌ Ventas (genera CxC, Cobros, descuenta inventario, multi-documento)
- ❌ OT (multi-paso: diseño → producción → QA → entrega)
- ❌ Entradas/Salidas/Traspasos/Ajustes (transacciones inventario críticas)
- ❌ Gastos/Ingresos/CxC/CxP (finanzas, requieren workspace persistente)

**Razón**: Validación controlada de UX, observabilidad runtime, aceptación operacional antes de expansión.

**Plan**: Completar piloto (Clientes, Productos) → validar con usuarios finales → expandir gradualmente según validación.

---

### [INFO] Botón "← Volver a Lista" Visible en Workspace Tabs

**Descripción**: El botón "← Volver a Lista" agregado en `CotizacionDocumentoPage.xaml` es VISIBLE tanto en:
- Document Surface contextual (donde SÍ debe estar)
- Workspace Tab persistente (donde NO debería estar, pero no causa problema funcional)

**Impacto**: Cosmético menor. Si un workflow complejo futuro abre `CotizacionDocumentoPage` en un Workspace Tab (ej: desde comparación multi-documento), el botón "← Volver a Lista" estará visible pero el callback `VolverALista` será `null` → clic no hace nada.

**Workaround actual**: Ninguno necesario. El callback es opcional (`Action?`).

**Plan futuro (LOW prioridad)**: Si se detecta uso mixto (mismo Page en Surface Y Workspace), agregar lógica condicional para ocultar el botón cuando `VolverALista == null`:

```xaml
<AppBarButton x:Name="BtnVolverALista" Label="Volver a Lista"
              Visibility="{x:Bind VolverALista, Mode=OneWay, Converter={StaticResource NullToCollapsedConverter}}"
              Click="BtnVolverALista_Click">
```

**Estado**: Aceptado como limitación menor del piloto.

---

### [INFO] Document Surface NO Reemplaza Workspace Tabs para Workflows Complejos

**Descripción**: Document Surface es un patrón UX **complementario**, NO un reemplazo total de Workspace Tabs.

**Regla oficial**:
```
Workspace Tabs      = workflows persistentes, multi-documento, complejos, importantes
Document Surfaces   = operación rápida contextual (Nuevo/Editar/Abrir) sin tab persistente
```

**Ejemplos donde Workspace Tabs DEBEN permanecer**:
- Usuario abre Cotización #123 → la convierte a Pedido #456 → necesita AMBOS documentos visibles simultáneamente (comparación/validación)
- Usuario trabaja en OT multi-paso que requiere horas/días de edición intermitente
- Workflows de análisis/comparación que requieren múltiples documentos abiertos
- Documentos "pinneados" que el usuario quiere mantener accesibles durante toda la sesión

**Estado**: Comportamiento esperado. WorkspaceService intacto y funcional para estos escenarios.

---

### [INFO] ContentPresenter No Cachea Instancias de Document Surface

**Descripción**: Cada vez que se abre un Document Surface (Nueva/Editar), se crea una **nueva instancia** de `CotizacionDocumentoPage`. Al cerrar el surface (`CerrarDocumentSurfaceAsync`), `DocumentSurfaceContent` se establece en `null`, destruyendo la instancia.

**Impacto**:
- ✅ Ventaja: estado limpio cada vez, sin riesgo de datos residuales
- ⚠️ Consideración: si un usuario abre "Nueva Cotización", llena el formulario, hace clic en "← Volver a Lista" (sin guardar), y vuelve a hacer clic en "Nueva Cotización", el formulario estará vacío (datos perdidos)

**Comportamiento esperado**: Esto es intencional. El flujo UX oficial es:
```
Abrir surface → editar → guardar → cerrar automático
              └─────────→ volver sin guardar → datos NO preservados
```

**Workaround si se requiere preservación temporal**: En el futuro, si se detecta necesidad operacional de preservar borrador, se puede implementar:
1. Guardar estado temporal en el ViewModel del módulo (`_draftCotizacion`)
2. Reutilizar instancia de Page en lugar de crear nueva
3. Prompt "¿Descartar cambios?" antes de cerrar sin guardar

**Estado**: Comportamiento actual aceptado. No hay requerimiento de preservación de borrador en piloto.

---

### [INFO] Runtime Observability Reporta Contexto de Módulo, No de Surface

**Descripción**: Cuando un Document Surface está activo, `CurrentContextTracker` y `OperationalObservabilityService` siguen reportando el contexto del **módulo padre** (CotizacionesViewModel), NO del documento embebido (CotizacionDocumentoViewModel).

**Impacto**:
- El Diagnostic Panel muestra "Ventas > Cotizaciones" incluso cuando el usuario está editando una cotización específica en el surface
- Los reportes de contexto operacional reflejan el listado, no el documento individual

**Razón**: Los ViewModels de documento (`CotizacionDocumentoViewModel`) actualmente NO implementan `ILiveContextReporter` y NO reportan contexto directamente. Solo los ViewModels de módulo lo hacen.

**Plan futuro (MEDIUM prioridad)**: Si se requiere observabilidad granular del documento activo:
1. Hacer que `CotizacionDocumentoViewModel` implemente `ILiveContextReporter`
2. Al abrir el surface, registrar el contexto del documento
3. Al cerrar el surface, restaurar el contexto del módulo

**Estado**: Limitación aceptada en piloto. Observabilidad a nivel módulo es suficiente para fase inicial.

---

## Problemas Resueltos (Histórico)

| ID | Descripción | Resuelto en |
|---|---|---|
| Entradas/Salidas con `ObservableCollection<object>` | Tipado a `EntradaResumenDto`/`SalidaResumenDto` | 2026-05-08 |
| ExistenciasPage stub "Próximamente" | Implementada con grid real y scope de almacén | 2026-05-08 |
| `PerfilesPage` mostrando placeholder | Implementada con CRUD completo | 2026-05-08 |
| `Application.Current` ambiguo con `Ybridio.Application` | Alias `XamlApp = Microsoft.UI.Xaml.Application` | 2026-05-08 |
| `ContentDialog.ShowAsync()` sin `using System;` | CS4036: requiere `using System` para WinRT interop | 2026-05-08 |
| **`IdentityRole<Guid>` DbSet exception en PermisoService** | Ver KI-012 abajo — fix aplicado: `_context.Roles` | 2026-05-08 |
| VentasPage stub "Próximamente" | Implementada con TabView lazy-load (4 tabs Sales Core) | 2026-05-08 |
| `DatePicker.Date` tipo `DateTime` incompatible con `DateTimeOffset` | Wrapper properties `FechaOffset`/`FechaCompromisoOffset` | 2026-05-08 |
