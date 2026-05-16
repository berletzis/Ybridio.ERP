# DECISIONS.md — Registro de Decisiones Arquitectónicas

> Este documento registra las decisiones técnicas importantes tomadas durante el desarrollo de Ybridio ERP,
> incluyendo la alternativa descartada y la razón de la elección.  
> Última actualización: 2026-05-15 (ver `SESSION_2026-05-15_CONSOLIDACION_FINANCIERA_Y_ESTABILIZACION.md`)

---

## ADR-070 — Header Folio Institucional en Document Surface

**Contexto**: `PedidoDocumentoPage` mostraba el folio solo en modo inline (HeaderStrip `Visibility="Collapsed"` cuando no inline). En ventana standalone no había identidad documental visible. `CotizacionDocumentoPage` tiene header siempre visible.

**Decisión**: Toda Page de Document Surface debe tener su `HeaderStrip` SIEMPRE visible. `EsInlineMode` solo controla los botones de navegación inline (BtnVolverALista, BtnAbrirEnVentana), nunca la visibilidad del header. Formato: `"Pedido PED-000020"` con badge de estado. Padding = `ErpDocumentHeaderPadding`.

---

## ADR-069 — Selector Institucional en todos los documentos comerciales

**Contexto**: `PedidoDocumentoPage.MostrarDialogoNuevaLinea()` usaba TextBoxes manuales sin autocomplete, mientras `CotizacionDocumentoPage` tenía `AutoSuggestBox` con `IProductoService.BuscarAsync`. Pedido insertaba líneas sin `ProductoId` correcto.

**Decisión**: Todos los Document Surface comerciales deben usar el mismo selector institucional con `AutoSuggestBox + ProductoSuggestion wrapper`. `PedidoDocumentoPage` ahora usa `_productoService.BuscarAsync` idéntico a Cotización. `ProductoSuggestion` está en namespace `Ybridio.WinUI.Views.Ventas` accesible desde ambas Pages.

---

## ADR-068 — Configuración Externalizada (appsettings pattern)

**Contexto**: Credenciales de BD (`sa` password) hardcodeadas en `App.xaml.cs` y `ErpDbContextFactory.cs`. Visibles en historial de git.

**Decisión**: Connection string vive en `appsettings.Development.json` (gitignored). Plantilla sin credenciales en `appsettings.json` (en repositorio). `ConfigurationBuilder + AddJsonFile + AddEnvironmentVariables` en `App.xaml.cs`. `ErpDbContextFactory` usa variable de entorno `ERP_CONNECTION_STRING` con fallback a appsettings. Paquetes: `Microsoft.Extensions.Configuration.Json`.

---

## ADR-067 — Pedido.Total debe incluir IVA (Total Institucional)

**Contexto**: `PedidoService` calculaba `Pedido.Total = SUM(Detalles.Importe) + SUM(Cargos.Importe)` sin IVA. El ViewModel calculaba `Total = Subtotal + OtrosCargos + Impuestos` con IVA. Cuando AnticipoPagado igualaba el total con IVA ($648.44), el servicio comparaba contra el Total sin IVA ($559.00) → falso SobrePagado de $89.44 = exactamente el IVA.

**Decisión**: `PedidoService` usa `RecalcularTotalConIva(p)` helper con `CommercialDocumentCalculator.CalcularImpuestos()` y `FiscalConstants.TasaIvaEstandar`. Todos los métodos que actualizan `p.Total` (AgregarDetalle, EliminarDetalle, AgregarCargo, EliminarCargo, RegistrarAnticipo) usan este helper. `RegistrarAnticipoAsync` recalcula en tiempo real antes de comparar para evitar valores stale de BD.

---

## ADR-066 — SafeFireAndForget Institucional

**Contexto**: Operaciones de hydration async (`HidratarSelectorClienteAsync`, `CargarConfiguracionFiscalAsync`) se ejecutaban como `_ = Task()` sin captura de excepciones. Fallos silenciosos no propagaban al usuario.

**Decisión**: Crear `SafeTaskExtensions.FireAndForget(onError, logger)` en `Ybridio.WinUI/Helpers/`. Usa `ContinueWith(OnlyOnFaulted)` para capturar y propagar errores via `onError` callback (típicamente `ViewModel.ErrorMessage`). NO reemplaza event handlers WinUI válidos (éstos son `async void` por diseño). Aplicar a toda operación fire-and-forget en Pages.

---

## ADR-065 — Anticipos sobre Pedidos (Dimensión Financiera Independiente)

**Contexto**: El Pedido es el momento donde se registra el compromiso financiero del cliente (anticipo), anterior a la Venta. El sistema no tenía entidad para registrar pagos parciales antes de generar la Venta.

**Decisión**: Nueva entidad `AnticipoPedido` (análoga a `PagoVenta`) con FK a Pedido. `EstadoFinancieroPedido` enum (6 estados) independiente de `EstatusPedido`. `PedidoService.CalcularEstadoFinanciero()` static como Single Source of Truth. Al generar Venta desde Pedido, `TotalPagado = pedido.AnticipoPagado` (anticipos consumidos). Permisos granulares: `anticipo.registrar`, `anticipo.configurar`. No usa `MovimientoCaja` (PYME simplicity).

---

## ADR-064 — Apertura Inline de Documentos Generados por Conversión

**Contexto**: Al convertir COT→PED, el Pedido abría en WorkspaceTab flotante en lugar del módulo PedidosPage, causando visual inconsistente (fondo transparente, layout diferente al abrir desde grid).

**Decisión**: Los documentos generados por conversión se abren INLINE en su módulo destino usando visual tree traversal. `EncontrarAncestro<VentasPage>()` localiza el módulo contenedor, que activa el tab correcto y delega a la página destino. Fallback a WorkspaceTab si VentasPage no está en el árbol.

---

## ADR-063 — AsNoTracking obligatorio en conversiones con EF Scoped Context

**Contexto**: `ConvertirAPedidoAsync` cargaba `CotizacionDetalle` con tracking normal. El `ErpDbContext` Scoped contenía versiones pre-descuento de las líneas (del history de edit operations). Al hacer `Include`, EF retornaba valores stale del identity map en lugar del DB.

**Decisión**: Cargar SIEMPRE detalles y cargos con `.AsNoTracking()` en conversiones. Solo la cabecera del documento origen se carga con tracking (para actualizar Estatus).

---

## ADR-062 — EF Core HasDefaultValue prohibido cuando valor = C# default

**Contexto**: `HasDefaultValue(0m)` en `PedidoDetalle.DescuentoPct` y `CotizacionDetalle.DescuentoPct` causaba que EF Core usara `ValueGenerated.OnAdd`, potencialmente omitiendo la columna del INSERT cuando el valor era el sentinel (0m para decimal).

**Decisión**: NO usar `HasDefaultValue` cuando el valor equals el C# type default. Sin `HasDefaultValue`, EF usa `ValueGenerated.Never` implícito y siempre incluye el campo. Los DEFAULT constraints del DB se gestionan solo en scripts SQL.

---

## ADR-061 — Page.Loaded Guard para NumberBox ValueChanged handlers

**Contexto**: En WinUI 3, cuando un DataTemplate renderiza un NumberBox con x:Bind Mode=TwoWay, dispara `ValueChanged` con OldValue=NaN durante la inicialización. Si el handler persiste a BD (delete+readd), corrompe datos con valores incorrectos.

**Decisión**: Flag `_listaParaEdicion = false` activado por `Page.Loaded` en TODAS las páginas con NumberBox DataTemplate que persisten a BD. Cualquier `ValueChanged` antes de `Loaded` es inicialización — no acción del usuario. Guard `if (!_listaParaEdicion) return` en todos los handlers afectados.

---

## ADR-060 — Session Closure Governance Policy

**Contexto:** El proyecto acumulaba deuda documental: código evolucionaba pero DECISIONS.md, CLAUDE_RULES.md y KNOWN_ISSUES.md quedaban desincronizados. El módulo de Auditoría Estructural generaba falsos positivos porque desconocía los cambios recientes.

**Decisión:** Política formal en `CLAUDE.md` bajo el nombre "Session Closure Governance Policy". Se activa con el trigger `Ejecutar Session Closure Review` o `Actualizar artefactos institucionales`.

**Proceso obligatorio:**
1. Análisis de impacto en 5 dimensiones: arquitectónico, workflow, runtime, auditoría, legacy
2. Actualización de 5 artefactos: ARCHITECTURE_STATUS.md, DECISIONS.md, KNOWN_ISSUES.md, CLAUDE_RULES.md, SESSION_*.md
3. Sección obligatoria "Impacto en Auditoría Estructural" cuando hay cambios de columnas/enums/lifecycle

**Restricciones críticas:**
- PROHIBIDO reparar findings automáticamente sin análisis
- PROHIBIDO reclasificar severidades sin justificación documentada
- PROHIBIDO asumir corrupción cuando puede ser estado legacy válido

**Objetivo:** Evitar *Architecture Drift* — que el código evolucione pero la documentación no.

---

## ADR-059 — Commercial Integrity Audit Pattern

**Contexto:** La auditoría de datos se enfocaba en catálogos legacy (migmap, dbo). El workflow comercial COT→PED→VTA→PAGO→CIERRE carecía de validación de consistencia financiera y coherencia entre documentos.

**Decisión:** Nuevo `ICommercialIntegrityAuditService` / `CommercialIntegrityAuditService` con 7 validadores:
- A: Cadena de conversión (Pedido→COT en estado Convertida, drift Total)
- B: Consistencia de totales financieros (Total encabezado = SUM detalles)
- C: Integridad de pagos (PagoVenta.Monto > 0, TotalPagado = SUM pagos)
- D: Aging operacional (documentos estancados con umbrales por tipo)
- E: Referencias cruzadas de productos en detalles
- F: Coherencia crédito/CxC
- G: Audit trail readiness

**Regla Module:** Todos los findings usan la propiedad `SchemaAuditEntry.Module` para grouping.
**UI:** Panel ejecutivo de chips por módulo. Filtro por módulo en CommandBar. Columna Módulo en grid.

---

## ADR-058 — ERP Structural Integrity Engine (Reclasificación Auditoría)

**Contexto:** El módulo de Auditoría generaba ~60 Critical por FK faltantes en BD (EF vs scripts manuales) y FK dbo legacy. Desconocía el workflow comercial, datos legacy válidos y migraciones manuales.

**Decisión:** Evolución de 3 capas:

1. **Nuevas severidades** en `AuditSeverity`:
   - `LegacyData = 4` — dato histórico válido, no requiere acción urgente
   - `MigrationPending = 5` — script manual no ejecutado

2. **Reclasificación `SchemaAuditService`:**
   - FK faltante en BD → `MigrationPending` (era `Critical`)
   - Columna faltante → `MigrationPending` con nombre de script conocido
   - Tablas dbo/migmap conocidas → `LegacyData` (era `Warning`)
   - Tipo incompatible → mantiene `Critical`

3. **Nuevo `WorkflowAuditService`:** 8 validadores — lifecycle COT/PED/VTA, snapshots, scripts pendientes, folios.

**`SchemaAuditEntry.Module`:** Propiedad opcional para agrupar findings por módulo de negocio.

**Resultado:** 0 Critical reales en YBRIDIO-26 (era ~60 falsos positivos).

---

## ADR-057 — Workflow Comercial — Estados + Folios + Bloqueo + Cierre

**Contexto:** Workflow comercial (COT→PED→VTA) tenía estados incompletos, folios ausentes en Pedido y Venta, y sin bloqueo operacional por estado.

**Decisiones:**

**EstatusPedido** (valores DB sin cambio para compatibilidad):
- `Borrador=0` (era Nuevo), `Autorizado=1` (era Confirmado), `EnProceso=2`, `Finalizado=3` (era Completado), `Parcial=4` (nuevo), `Cancelado=9`

**EstatusVenta** (valores DB sin cambio):
- `Borrador=0`, `PendientePago=1` (era Confirmada), `Pagada=2`, `Facturada=3`, `Entregada=4`, `Cerrada=5`, `Cancelada=9`

**Folio en Pedido y Venta:** `PedidoService.CrearAsync` y `VentaDocumentalService.CrearAsync` invocan `IFolioGeneratorService` (Document Identity Rule: folio propio por tipo de documento).

**Auto-transición Pagada:** `RegistrarPagoAsync` transiciona automáticamente `PendientePago → Pagada` cuando `TotalPagado >= Total`.

**CerrarVenta:** Nuevo `CerrarAsync` en `IVentaDocumentalService`. Valida saldo=0 antes de transicionar a `Cerrada`.

**Bloqueo por estado:**
- Cotización Aprobada: congela líneas/precios/descuentos/impuestos
- Pedido Finalizado/Cancelado: bloquea edición
- Venta Cerrada/Cancelada: bloquea todas las operaciones

**Status capsules:** Colores dinámicos por estado vía converters `EstatusPedidoBadgeConverters` y `EstatusVentaBadgeConverters`. CommandBar con `IsEnabled` por workflow guard.

---

## ADR-050 — Singleton Operational Surface Pattern

**Contexto:** EmpresaPage (y futuras pantallas singleton) tenían layout aislado que rompía la consistencia visual del ERP.

**Decisión:** Toda pantalla de entidad singleton adopta el mismo patrón operacional que grids multi-registro:
- CommandBar institucional (Editar / Guardar / Cancelar / Actualizar)
- Grid izquierdo con el registro único visible
- Surface derecho con formulario (read-only o editable según IsEditing)
- Snapshot pattern para cancelar

**Anti-patrón eliminado:** ScrollViewer + StackPanel MaxWidth=600 + botón flotante aislado.

**Aplica a:** Empresa, ConfiguraciónFiscal, ConfiguraciónSistema, PreferenciasGlobales.

---

## ADR-051 — Shared Sequence/Folio Pattern

**Contexto:** Folios documentales hardcodeados o inexistentes. `FiscalConstants.TasaIvaEstandar` como único control de IVA.

**Decisión:** `SerieDocumento` como entidad propia con generación atómica vía SQL:
```sql
UPDATE catalogos.SerieDocumento SET SiguienteNumero = SiguienteNumero + 1
OUTPUT DELETED.SiguienteNumero WHERE Id = @id
```
`IFolioGeneratorService` usa `IDbContextFactory` (contexto aislado) para seguridad ante concurrencia. `.ToListAsync()` antes de acceder al resultado para evitar composición SQL sobre sentencias DML.

**Document Identity Rule:** Cada conversión documental genera folio NUEVO independiente. `COT-000001 → PED-000001 → VTA-000001`. Trazabilidad vía referencias cruzadas, nunca folios compartidos.

**Anti-patrones prohibidos:** Reutilizar folio entre conversiones. Usar ParametroGlobal para consecutivos runtime.

---

## ADR-052 — Commercial Tax Pattern (Single Source of Truth Fiscal)

**Contexto:** Ambigüedad entre catálogo fiscal (TipoImpuesto) y parámetros operacionales. Existencia de `FiscalConstants.TasaIvaEstandar = 0.16m` hardcodeado en múltiples lugares.

**Decisión — Una sola fuente de verdad fiscal:**
```
TipoImpuesto (catálogo)  →  QUÉ impuestos existen + cuál es su tasa
ParametroGlobal (config) →  CUÁL TipoImpuesto usar por default (almacena TipoImpuestoId como int)
IConfiguracionFiscalService → resuelve ParametroGlobal → TipoImpuesto → Tasa decimal
FiscalConstants.TasaIvaEstandar → FALLBACK únicamente, documentado como tal
```

**ParametrosClave:** Constantes tipadas análogas a PermisosClave. `ParametrosClave.Fiscal.ImpuestoDefaultProducto = "impuesto.default.producto"`. NUNCA strings literales en código.

**Concurrencia:** `ConfiguracionFiscalService` usa `IDbContextFactory` (contexto propio aislado) — aplica ADR-026 a servicios de configuración.

**Anti-patrones prohibidos:** `iva.tasa.default = "0.16"` (tasa en texto). Duplicar tasas en ParametroGlobal. Múltiples fuentes fiscales.

---

## ADR-053 — Product Type Classification Pattern

**Contexto:** TipoProducto solo tenía `Nombre`. No había forma de distinguir programáticamente Inventariable vs Servicio.

**Decisión:** Campo `Clave` operacional (max 10, ej: PROD, SERV, REF, EQP, LIC, MOB) como identificador humano en reglas de negocio. El `Id` es técnico y NO reemplaza la Clave operacional.

**Regla institucional:** Los Servicios son Productos con `TipoProducto.Clave = "SERV"`. No existe tabla `Servicios` separada.

---

## ADR-054 — Commercial Charges Pattern

**Contexto:** Necesidad de cargos accesorios (Flete, Maniobras, Seguro) en documentos comerciales. Prohibido representarlos como Productos inventariables.

**Decisión:** `CotizacionCargo` como entidad propia (child record de Cotizacion, sin AuditableEntity). Sección visual separada en el documento. Cargos impactan IVA y Total del documento:
```
Total = Subtotal(productos) + OtrosCargos + IVA(productos con IVA + cargos con AplicaIva)
```
Cargos en memoria (IsNuevo=true) se persisten en batch después de `CrearAsync`, antes de `Initialize()`.

**Anti-patrón eliminado:** Usar Productos con flag especial para representar cargos documentales.

---

## ADR-055 — Single Document Scroll Pattern

**Contexto:** `CotizacionDocumentoPage` tenía `Height="*"` en la fila de productos y `MaxHeight="200"` en OtrosCargos, causando scroll interno en ambos grids.

**Decisión — Estructura obligatoria en superficies documentales:**
```
Row 0 (Auto): CommandBar — fijo, siempre visible
Row 1 (*):    ScrollViewer — ÚNICO dueño del scroll documental
Row 2 (Auto): StatusBar — fijo, siempre visible
```
Dentro del ScrollViewer: Grid con TODAS las filas en `Auto`. ListViews con:
```xml
ScrollViewer.VerticalScrollBarVisibility="Disabled"
ScrollViewer.VerticalScrollMode="Disabled"
```

**Resultado:** Los grids crecen dinámicamente con su contenido. El usuario hace scroll sobre el documento completo, nunca dentro de grids pequeños.

**Anti-patrones prohibidos:** `Height="*"` en filas de grids documentales. `MaxHeight` arbitrario en secciones documentales. Nested ScrollViewers. Scroll interno en grids operacionales.

**Aplica a:** Cotizaciones, Pedidos, Ventas, OrdenesTrabajo, documentos futuros.

---

## ADR-056 — Global Document Runtime Ownership Pattern

**Contexto:** Single Document Session Rule se rompía entre hosts (tab vs ventana detached) para documentos nuevos recién guardados.

**Causa raíz dual:**
1. `_cotizacionOriginal` (readonly) permanecía `null` para docs nuevos aunque ya guardados → window key usaba `_sessionKey` (GUID) → `TryActivateWindow` no encontraba la ventana
2. `_currentInlineDocumentId` no se actualizaba tras primer guardado → check 2 fallaba → segunda instancia inline

**Decisión:**
- `ViewModel.DocumentoId` (`public long? DocumentoId => _documento?.Id`) expuesta para acceso desde la Page
- Window key usa `ViewModel.DocumentoId?.ToString() ?? _sessionKey` (no `_cotizacionOriginal?.Id`)
- `DocumentSaved` callback en `BtnNueva_Click` actualiza `_currentInlineDocumentId = page.ViewModel.DocumentoId`

**Invariante:** Un documento comercial existe como máximo UNA vez en toda la aplicación, sin importar si está en tab inline, ventana detached, o combinación de ambos.

---

## ADR-043 — Runtime Persistence, Client Chip & Discount Alert Lifecycle

**Decisión**: Corrección operacional incremental del módulo Cotizaciones — runtime state, cliente visible, descuento sin alerta duplicada, DatePicker compacto, command surface limpio.

### Reglas establecidas

**Chip de cliente**:
- `Initialize()` sintetiza `_entidadDirectorioSeleccionada` desde `RelacionComercialId` + `NombreCliente` del documento existente.
- Permite que `EntidadDirectorioSeleccionada` sea no-null en modo edición y en detach/rehost sin reejecutar Initialize().
- El chip siempre lee de `ViewModel.EntidadDirectorioSeleccionada`, NUNCA del textbox superior.

**Guard de GetOrCreate**:
- Se añade `_clienteModificadoPorUsuario` (bool). Solo se activa en `SeleccionarCliente()` y `LimpiarCliente()`.
- `GuardarAsync` llama a `GetOrCreate` **solo cuando** `_clienteModificadoPorUsuario = true`.
- Documentos existentes sin cambio de cliente preservan `RelacionComercialId` sin renegociar con el servicio.

**Alerta de descuento global**:
- La alerta "Aplicar descuento global eliminará los descuentos individuales" se muestra **solo** cuando:
  1. El usuario cambia activamente el `NumberBox` de descuento global, Y
  2. `HayDescuentosEnLineas = true`.
- Nunca se muestra: al abrir, editar, rehidratar, detach/rehost del documento.
- Guard: `_hidratandoUI = true` antes de `InitializeComponent()`, `false` después de toda la inicialización/rehost. `NbDescuentoGlobal_ValueChanged` retorna inmediatamente si `_hidratandoUI`.

**DatePicker compacto (token)**:
- `ErpDateFieldWidth` reducido de 220 → 185 en `FormBase.xaml`.
- Mantiene visibilidad de día/mes/año en locale español, aspecto más compacto e institucional.

**Command surface separation**:
- `OverflowButtonVisibility="Collapsed"` en el CommandBar elimina el botón "...".
- `BtnAbrirEnVentana` movido a `CommandBar.Content` (zona izquierda de la barra que queda libre), alineado a la derecha. Visualmente separado como acción workspace/window, no operacional.

**Anti-patrones eliminados**:
- Ya no se reinstancia la entidad de Directorio desde el constructor de la página (constructor anterior creaba un `DirectorioSelectorDto` inline en la página, no en el ViewModel).
- Ya no se llama GetOrCreate en GuardarAsync para documentos existentes sin cambio de cliente.
- Ya no se muestra la alerta de descuento al abrir o rehidratar documentos con descuento global previo.

---

## Single Document Session Rule — Un documento, una sesión runtime

**Problema**: Al abrir una Cotización en una ventana detached, el Document Surface inline se cerraba (correcto). Pero si el usuario intentaba re-abrir el mismo documento desde el grid, el sistema creaba una nueva instancia ignorando que ya existía una sesión activa en la ventana OS real → dos ViewModels, dos dirty states, conflicto de edición.

**Regla institucional**: Un documento comercial NO puede existir simultáneamente en múltiples sesiones runtime independientes. Desacoplar una ventana NO crea nueva sesión — solo cambia el host visual. La sesión runtime (ViewModel, estado, dirty) sigue siendo única.

**Implementación**:

1. **`IWindowManager.TryActivateWindow(string documentKey)`** — busca en el registro de ventanas activas cualquier entrada cuya key interna termina con `_{documentKey}`. Si existe: activa/enfoca la ventana y retorna `true`. Si no: retorna `false`.

2. **`CotizacionesPage._currentInlineDocumentId`** — rastrea el ID del documento actualmente en el Document Surface inline.

3. **`AbrirCotizacionInline`** — aplica Single Document Session Rule antes de crear cualquier instancia:
   ```
   1. ¿En ventana detached? → TryActivateWindow → return (no nueva instancia)
   2. ¿Ya inline con mismo ID? → return (ya visible)
   3. Sin sesión → crear nueva sesión normalmente
   ```

**Clave de convención**: `detached:{tipo}:{id}` (ej: `detached:cotizacion:123`)  
**Key interna de WindowManager**: `DetachedDocumentWindow_detached:cotizacion:123`

**Diseñado para reutilización**: `TryActivateWindow` en `IWindowManager` funciona para cualquier tipo de documento. Aplicar el mismo patrón en `PedidosPage`, `VentasPage`, `OrdenesTrabajoPage`.

---

## Commercial Document Workflow Pattern — Separación Estados vs Acciones Operacionales

**Problema**: El modelo anterior mezclaba estados comerciales con acciones operacionales. "Enviada" como estado comercial generaba restricciones incorrectas: "solo se puede aprobar desde Enviada" y "solo se puede enviar desde Borrador" — contradiciendo la realidad operacional donde una cotización aprobada puede enviarse múltiples veces.

**Decisión**: Separar formalmente **estados comerciales** de **acciones operacionales**.

### Estados comerciales (EstatusCotizacion)

```
Borrador → Aprobada → Convertida  (terminal)
              ↘
              Cancelada            (terminal)
```

| Estado | Valor | Descripción |
|---|---|---|
| Borrador | 0 | En edición — estado inicial |
| ~~Enviada~~ | 1 | **LEGACY** — mantenido para compatibilidad BD. Tratar como Aprobada |
| Aprobada | 2 | Aprobada comercialmente |
| Convertida | 3 | Convertida a Pedido — terminal |
| Cancelada | 9 | Cancelada — terminal |

### Acciones operacionales (NO modifican estado)

- **Guardar**: persiste cambios, no cambia estado
- **Enviar**: acción operacional (V1: stub; Future: email/PDF/auditoría). Disponible desde cualquier estado activo — una cotización Aprobada puede enviarse múltiples veces
- **Duplicar** (futuro), **Imprimir** (futuro)

### Acciones de workflow (SÍ modifican estado)

- **Aprobar**: Borrador → Aprobada
- **Convertir a Pedido**: Aprobada → Convertida (también crea el Pedido)
- **Cancelar**: cualquier estado activo → Cancelada

### Guardas correctas

```csharp
PuedeEnviar   = !IsNuevo && Estatus is not (Cancelada or Convertida)  // Acción operacional
PuedeAprobar  = !IsNuevo && Estatus == Borrador                        // Workflow
PuedeConvertir = !IsNuevo && Estatus == Aprobada                       // Workflow
PuedeCancelar = !IsNuevo && Estatus is not (Cancelada or Convertida)  // Workflow
PuedeEditar   = Estatus is not (Cancelada or Convertida)
```

### CommandBar grouping

```
[Guardar] [—] [Agregar] [Eliminar] [—] [Aprobar] [Convertir] [Cancelar] [—] [Enviar]
   ↑ persistencia  ↑ líneas    ↑────── workflow comercial ──────↑   ↑ operacional
```

### Diseñado para reutilización

El patrón aplica a: Cotizaciones ✅ | Pedidos 🔲 | Ventas 🔲 | OrdenesTrabajo 🔲

---

## Operational Date Display Pattern — CalendarDatePicker + etiqueta legible

**Decisión**: Reemplazar `DatePicker` (spinner de columnas, propenso a clipping) por `CalendarDatePicker` + etiqueta textual operacional "8 Junio 2026".

**Estructura**: CalendarDatePicker como input (OneWay + handler), etiqueta como representación oficial visible (x:Bind + OperationalDateConverter). Los dos son complementarios — el picker es mecanismo de selección, la etiqueta es el texto legible.

**`OperationalDateConverter`** (`Converters/OperationalDateConverter.cs`): convierte `DateTimeOffset`/`DateTimeOffset?`/`DateTime` → "8 Junio 2026". `FormatOperationalDate()` también disponible como método estático para uso programático.

**Por qué CalendarDatePicker**: más compacto, abre calendario visual completo, sin problemas de clipping de columnas. `Date` es `DateTimeOffset?` — compatible con OneWay binding desde propiedades `DateTimeOffset` del ViewModel.

**Por qué OneWay + handler**: `CalendarDatePicker.Date` (nullable) + ViewModel (non-nullable) requieren conversión explícita. El handler `DateChanged` propaga el cambio limpiamente.

---

## Selector DTO Hydration Rule — Single Source of Truth para entidad seleccionada

**Problema**: `Initialize()` creaba un `DirectorioSelectorDto` sintético con tres errores:
1. `EntityType = Empresa` hardcodeado — incorrecto para Personas
2. `EmpresaComercialId = RelacionComercialId` — mapping erróneo (`RelacionComercialId` ≠ `EmpresaComercialId`)
3. Sin Email, Teléfono ni RFC — metadata vacía

**Solución: Hydration en dos fases**:
- **Fase 1 (síncrona, en constructor)**: DTO sintético mínimo para display inmediato del nombre
- **Fase 2 (async, fire-and-forget)**: `HidratarSelectorClienteAsync` carga el DTO real desde BD con `IDirectorioService.ObtenerDtoParaSelectorAsync`, actualiza ViewModel y chip visual

**`ObtenerDtoParaSelectorAsync(relacionComercialId)`** (nuevo en `IDirectorioService`):
- Carga `RelacionComercial` con `Include(Persona)` + `Include(EmpresaComercial)`
- Determina el tipo correcto según qué navegación está set
- Devuelve DTO con todos los campos completos

**`RestaurarEntidadSeleccionada(dto)`** (nuevo en `CotizacionDocumentoViewModel`):
- Actualiza `_entidadDirectorioSeleccionada`, `NombreCliente`, `ClienteEmail`, `ClienteTelefono`
- **NO marca `IsDirty`** — es restauración de estado, no acción del usuario

**Rehost/Detach**: El ViewModel ya tiene el DTO hidratado de la página original. El constructor de rehost simplemente asigna `SelectorCliente.EntidadSeleccionada = ViewModel.EntidadDirectorioSeleccionada`. Sin re-carga.

**Regla institucional**: PROHIBIDO crear `DirectorioSelectorDto` manual con datos parciales. Usar `ObtenerDtoParaSelectorAsync` como Single Source of Truth.

---

## Global Discount Lifecycle — Discount Uniformity Rule & Invalidation Pattern

**Decisión**: El `DescuentoGlobalPct` en el ViewModel es un **indicador de uniformidad**, no un estado persistente. Representa "todas las líneas tienen el mismo % de descuento." En cuanto una línea individual diverge, el concepto global deja de ser semánticamente válido y se borra automáticamente.

**Regla de uniformidad**: El descuento global es válido SOLO cuando `∀ línea: línea.DescuentoPct == DescuentoGlobalPct`.

**Invalidation Pattern** (silencioso, sin alerta):
```csharp
// ViewModel — InvalidarDescuentoGlobal()
internal void InvalidarDescuentoGlobal()
{
    if (DescuentoGlobalPct != 0)
        DescuentoGlobalPct = 0;  // Solo borra el indicador, no los descuentos de línea
}

// Code-behind — NumberBox_Descuento_ValueChanged
if (nuevoPct != ViewModel.DescuentoGlobalPct)
    ViewModel.InvalidarDescuentoGlobal();  // Silencioso, sin diálogo
```

**Punto de entrada**: `NumberBox_Descuento_ValueChanged` — cuando el usuario edita manualmente el descuento de UNA línea con un valor diferente al global actual, se invalida automáticamente. Los descuentos de línea existentes NO se modifican.

**Guard de no-interferencia**: El guard `(decimal)args.NewValue == ViewModel.DescuentoGlobalPct` en el handler previene que `AplicarDescuentoGlobalALineas` (Phase 1) dispare la invalidación — solo cambios manuales del usuario llegan al código de invalidación.

**Semántica visual**: "Subtotal sin descuentos" (antes: "Subtotal bruto") y "Importe Neto" (antes: "Importe") hacen explícito que el importe ya incluye el descuento aplicado.

---

## ADR-043b — Two-Phase Discount Apply Pattern (Concurrencia DbContext)

**Problema**: `AplicarDescuentoGlobalALineas` para documentos EXISTENTES causaba `InvalidOperationException: "A second operation was started on this context instance"` al aplicar descuento global en documentos con múltiples líneas.

**Causa raíz**: La secuencia original llamaba `ActualizarDescuentoAsync` dentro del loop. Esta función establece `linea.DescuentoPct = pct` al final (después del delete+readd). Esa asignación dispara INPC sincrónicamente → NumberBox.ValueChanged → `NumberBox_Descuento_ValueChanged` (async void) → `ActualizarDescuentoAsync` de NUEVO. Mientras este segunda llamada hace `await EliminarDetalleAsync`, el loop principal ya avanzó a la siguiente iteración y también llama `EliminarDetalleAsync`. Dos operaciones concurrentes sobre el mismo `_context` scoped → excepción.

**Solución: Two-Phase Discount Apply Pattern**:

```
Fase 1 (síncrona, memoria):
  foreach linea → linea.DescuentoPct = pct
  INPC dispara NumberBox.ValueChanged → handler → ActualizarDescuentoAsync
      → guard: linea.DescuentoPct == pct → return early (sin service call)
  
Fase 2 (async, BD, único scope IsBusy):
  IsBusy = true
  foreach linea → EliminarDetalleAsync + AgregarDetalleAsync (secuencial)
  linea.Id = newId  ← solo actualizar Id, DescuentoPct ya está correcto
  IsBusy = false
```

**Tres layers de protección:**
1. `linea.DescuentoPct == pctClamped` guard en `ActualizarDescuentoAsync` — protección PRIMARIA contra re-entrada desde INPC
2. `if (ViewModel.IsBusy) return;` en `NumberBox_Descuento_ValueChanged` — defensa en profundidad (eventos concurrentes durante Fase 2)
3. `if (ViewModel.IsBusy) return;` en `NbDescuentoGlobal_ValueChanged` — evita iniciar operación global mientras service está ocupado

**Regla institucional (aplicar a futuros módulos)**:
> Todo operación que actualice múltiples líneas en documentos EXISTENTES debe seguir el Two-Phase Pattern: actualizar memoria primero (todos los valores), luego persistir secuencialmente bajo un scope IsBusy único.

**Archivos modificados**: `CotizacionDocumentoViewModel.cs` (AplicarDescuentoGlobalALineas, LimpiarDescuentoGlobal, ActualizarDescuentoAsync), `CotizacionDocumentoPage.xaml.cs` (NumberBox_Descuento_ValueChanged, NbDescuentoGlobal_ValueChanged).

---

## ADR-042 — Commercial Discount Pattern

**Decisión**: Descuentos comerciales (por línea y global) para documentos comerciales del ERP — Cotizaciones como piloto, reutilizable en Pedidos, Compras, Ventas, Facturación.

**Regla No Acumulable**: Descuento global y descuentos individuales por línea son MUTUAMENTE EXCLUYENTES. Si el usuario aplica un descuento global cuando existen descuentos por línea, se muestra confirmación institucional antes de proceder. Al confirmar, los descuentos individuales se eliminan y el global se aplica a todas las líneas.

**Fórmula oficial (Single Source of Truth)**:
```
ImporteNeto = Cantidad × PrecioUnitario × (1 − DescuentoPct / 100)
```
El descuento se aplica **antes** del IVA. El IVA se calcula sobre el importe neto.

**CommercialDocumentCalculator** (`Ybridio.Application.Common`): clase estática reutilizable con métodos puros. Toda aritmética comercial debe derivar de aquí. NUNCA duplicar la fórmula.

**Totales del documento**:
```
SubtotalBruto  = SUM(Cantidad × PrecioUnitario)         ← visible solo cuando HayDescuento
DescuentoTotal = SubtotalBruto − Subtotal               ← visible solo cuando HayDescuento
Subtotal       = SUM(ImporteNeto)                       ← siempre visible
Impuestos      = SUM(ImporteNeto líneas IVA) × TasaIva  ← siempre visible
Total          = Subtotal + Impuestos                   ← siempre visible
```

**Persistencia**: `DescuentoPct` se almacena por línea en `ventas.CotizacionDetalle`. Script de BD: `Documentation/Scripts/AddDescuentoPct_CotizacionDetalle.sql`. El descuento global no tiene columna propia; se detecta al cargar si todas las líneas tienen el mismo porcentaje.

**UI**: columna `Desc. %` editable inline (NumberBox) en el grid de líneas. Bloque `Descuento Global (%)` en el formulario de encabezado (Col 1, Row 1). Fix cliente en edición: se restaura el chip del selector via `DirectorioSelectorDto` sintético sin disparar `SelectionChanged`.

**DatePicker fix**: `HorizontalAlignment="Stretch"` en ambos DatePickers garantiza que el ancho del token `ErpDateFieldWidth=220` se aplique al control interno, mostrando el año completo.

**Archivos clave**:
- `Ybridio.Application/Common/CommercialDocumentCalculator.cs` — SoT cálculos
- `Ybridio.Domain/Ventas/CotizacionDetalle.cs` — campo `DescuentoPct`
- `Ybridio.Application/DTOs/Ventas/CotizacionDto.cs` — DTOs actualizados
- `Ybridio.Application/Services/Venta/CotizacionService.cs` — usa Calculator
- `Ybridio.WinUI/ViewModels/Ventas/CotizacionDocumentoViewModel.cs` — `DetalleLineaEditable` + `DescuentoGlobalPct`
- `Ybridio.WinUI/Views/Ventas/CotizacionDocumentoPage.xaml` — columnas + bloque global + totales
- `Ybridio.WinUI/Views/Ventas/CotizacionDocumentoPage.xaml.cs` — handlers + fix selector edición

**Alternativas descartadas**:
- Descuentos acumulables (global + línea): descartado — ambigüedad fiscal y de UX inaceptable para PYME.
- Almacenar descuento global en columna propia de `Cotizacion`: descartado — scope incremental; detectar heurísticamente al cargar es suficiente para V1.
- Modal pesado para editar descuento: descartado — edición inline en grid es más rápida (ERP operacional).

---

## ADR-041 — Operational Editable Document Lines Pattern

**Decisión**: Las líneas de un documento comercial (Cotización) deben ser editables inline sin abrir ningún diálogo pesado. La columna `Cantidad` usa un `NumberBox` inline. Toda modificación recalcula importe de línea y totales del documento de forma inmediata. `DetalleLineaEditable` implementa `INotifyPropertyChanged` para que el grid refleje cambios en tiempo real.

**Revisión 2 (correctivos operacionales)**:
- Cantidad = 0 en `NumberBox`: muestra confirmación antes de eliminar; si el usuario cancela, restaura el valor anterior sin efectos secundarios.
- Botón eliminar por línea (columna `⊗` de 40px, `ErpActionButtonStyle` con glyph de papelera) con confirmación institucional (`ContentDialog`).
- `TotalArticulos = SUM(Cantidad)` expuesto como `ObservableProperty` en el ViewModel, notificado en `RecalcularTotales()`.
- Status bar actualizado: muestra `N línea(s) • M artículo(s)` usando tokens `ErpGridStatusTextStyle`.
- DatePicker `Fecha` y `Vigencia` cambiados de `Width="180"` a `Width="220"` para mostrar el año completo sin truncamiento.

**Problema identificado**:
- `DetalleLineaEditable` era un POCO sin notificaciones: la columna `Importe` no se actualizaba visualmente al cambiar la cantidad.
- La cantidad solo podía editarse reabriendo el diálogo completo del producto — experiencia lenta y no ERP-nativa.
- Los totales del documento se actualizaban correctamente, pero la línea visual quedaba desfasada (subtotal mostraba valor correcto, pero el importe de la fila mostraba el valor anterior).
- Existían múltiples rutas de cálculo de importe: en el POCO, en el ViewModel, en el servicio — sin Single Source of Truth.

**Reglas institucionales ADR-041 (OBLIGATORIAS)**:
> `DetalleLineaEditable` DEBE implementar `INotifyPropertyChanged`.  
> `Importe` es propiedad calculada (`Cantidad × PrecioUnitario`) — NUNCA almacenada separada en el modelo editable.  
> `Cantidad` y `PrecioUnitario` DEBEN notificar cambios de `Importe` vía `PropertyChanged`.  
> Toda edición de cantidad DEBE llamar a `RecalcularTotales()` vía `CantidadCambiadaCallback`.  
> NUNCA abrir modal pesado solo para editar cantidad.  
> `ActualizarCantidadAsync(linea, nuevaCantidad)` es el único entry point para modificar cantidad.  
> Cantidad negativa → ignorar silenciosamente.  
> Cantidad = 0 → eliminar línea automáticamente.  
> Para documentos existentes → persistir inmediatamente vía eliminar + reagregar detalle en BD.

**Solución adoptada**:
- `DetalleLineaEditable` convertido a `INotifyPropertyChanged` con backing fields `_cantidad` y `_precioUnitario`.
- `Cantidad` y `CantidadDouble` (wrapper `double` para `NumberBox`) notifican `Cantidad`, `CantidadDouble` e `Importe`.
- `CantidadCambiadaCallback` (internal `Action?`) conectado por `WirarLinea()` para disparar `IsDirty = true` + `RecalcularTotales()`.
- `WirarLinea(DetalleLineaEditable linea)` — helper del ViewModel que asigna el callback y retorna la línea. Llamado en `Initialize()` y `AgregarDetalleLocalAsync()`.
- `ActualizarCantidadAsync(linea, nuevaCantidad)` — lógica centralizada de actualización: valida negativo/cero, aplica en memoria (nuevo doc) o persiste en BD (doc existente).
- `IncrementarCantidadAsync` delegado a `ActualizarCantidadAsync`.
- XAML `DataTemplate`: `Cantidad` es `NumberBox` con `Value="{x:Bind CantidadDouble, Mode=TwoWay}"`, `Minimum=0`, sin spin-buttons. `Importe` y `PrecioUnitario` usan `Mode=OneWay`.
- `NumberBox_Cantidad_ValueChanged` en code-behind: solo persiste en BD cuando `!ViewModel.IsNuevo`. Para nuevos docs, el binding TwoWay + INPC ya aplica el cambio.

**Alternativas descartadas**:
- Modal de edición de cantidad: descartado — excesiva fricción operacional para POS/ERP.
- `ObservableCollection<DetalleLineaEditable>` solo para trigger de UI: descartado — la colección notifica inserciones/eliminaciones pero no mutaciones de propiedades internas; se requiere INPC en el item.
- Recalcular totales solo en CollectionChanged: descartado — no detecta cambios de cantidad en líneas existentes.

**Archivos clave**:
- `Ybridio.WinUI/ViewModels/Ventas/CotizacionDocumentoViewModel.cs` — `DetalleLineaEditable`, `WirarLinea`, `ActualizarCantidadAsync`.
- `Ybridio.WinUI/Views/Ventas/CotizacionDocumentoPage.xaml` — inline `NumberBox` quantity editor.
- `Ybridio.WinUI/Views/Ventas/CotizacionDocumentoPage.xaml.cs` — `NumberBox_Cantidad_ValueChanged` handler.

---

## ADR-040 — Operational Commercial Document Standard

**Decisión**: La Cotización debe comportarse como documento comercial ERP estándar con comportamiento mínimo operacional: sin líneas duplicadas del mismo producto, recálculo automático de totales, dirty-state explícito, confirmación de cierre protegida, visualización de IVA por línea, y desglose Subtotal / Impuestos / Total. No hay auto-save.

**Problema identificado**:
- El sistema creaba líneas duplicadas si se agregaba el mismo producto dos veces.
- El total solo mostraba Subtotal sin IVA ni Impuestos desglosados.
- No existía dirty-state ni confirmación de cierre para cambios no guardados.
- Los valores monetarios se mostraban sin formato de moneda ($#,##0.00).
- La tasa de IVA estaba hardcodeada o ausente en lugar de ser una constante centralizada.

**Reglas institucionales ADR-040 (OBLIGATORIAS)**:
> NUNCA crear líneas duplicadas del mismo producto — sumar cantidad sobre línea existente.  
> NUNCA auto-guardar al cerrar. La decisión es del usuario.  
> SIEMPRE mostrar confirmación institucional al cerrar un documento dirty.  
> SIEMPRE usar `FiscalConstants.TasaIvaEstandar` — NUNCA hardcodear 0.16.  
> Total = Subtotal + Impuestos. Impuestos = Σ(líneas con IVA) × TasaIvaEstandar.  
> IsDirty = true en toda mutación (agregar/eliminar/cambiar cantidad/cambiar cliente/observaciones).  
> IsDirty = false solo después de guardar exitosamente.

**Solución adoptada**:
- `FiscalConstants.TasaIvaEstandar = 0.16m` en `Ybridio.Domain/Common/FiscalConstants.cs`.
- `DetalleLineaEditable` extendido con `IvaAplicable` (del producto) e `IvaTexto` ("Sí"/"No").
- `CotizacionDocumentoViewModel` refactorizado con métodos pequeños y claros:
  - `ObtenerLineaExistente(productoId)` — busca línea existente del mismo producto.
  - `AgregarOIncrementarDetalleAsync(detalle)` — entry point principal: merge-or-add.
  - `IncrementarCantidadAsync(linea, incremento)` — suma cantidad, persiste si es doc existente.
  - `AgregarDetalleLocalAsync(detalle)` — agrega nueva línea (nuevo o existente).
  - `RecalcularTotales()` — único lugar de cálculo; llama a `CalcularSubtotal()` + `CalcularImpuestos()`.
  - `CalcularSubtotal()` — `SUM(Detalles.Importe)`.
  - `CalcularImpuestos()` — `SUM(líneas IVA) × FiscalConstants.TasaIvaEstandar`.
- `IsDirty` marcado en `SeleccionarCliente`, `LimpiarCliente`, `OnObservacionesChanged`, todas las operaciones de líneas. Reseteado en `Initialize()` y tras `GuardarAsync` exitoso.
- `CotizacionDocumentoPage` enruta `BtnAgregarLinea` a `AgregarOIncrementarDetalleAsync`.
- `MostrarConfirmacionCierreAsync()` — diálogo institucional: Guardar / No Guardar / Cancelar.
- `BtnVolverALista_Click` aguarda confirmación antes de ejecutar `VolverALista?.Invoke()`.
- XAML: columna IVA en grid, fila Impuestos en totales, `DecimalToCurrencyConverter` en todos los valores monetarios.

**Alternativas descartadas**:
- Motor fiscal complejo con múltiples tasas: descartado — solo se necesita simple IVA estándar en V1.
- Auto-save al cerrar: descartado — viola el principio de decisión explícita del usuario.
- Cálculo en UI (code-behind): descartado — lógica en ViewModel siguiendo Clean Architecture.

**Archivos clave**:
- `Ybridio.Domain/Common/FiscalConstants.cs` — constante TasaIvaEstandar
- `Ybridio.WinUI/ViewModels/Ventas/CotizacionDocumentoViewModel.cs` — lógica operacional
- `Ybridio.WinUI/Views/Ventas/CotizacionDocumentoPage.xaml` — IVA + totales + moneda
- `Ybridio.WinUI/Views/Ventas/CotizacionDocumentoPage.xaml.cs` — merge routing + close confirmation

---

## ADR-039 — Shared Document Session Pattern / Detach Host Swap

**Decisión**: El modo Detach ("Abrir en nueva ventana") NO recrea el documento ni el ViewModel. Rehostea la **misma instancia de página** en una ventana OS independiente. El estado runtime completo (cliente seleccionado, chip visual, líneas, totales, dirty state, validaciones, selecciones) se preserva íntegramente. No existe auto-save al desacoplar.

**Problema identificado**:
- La implementación anterior de `BtnAbrirEnVentana_Click` creaba `new CotizacionDocumentoPage(_cotizacionOriginal)` dentro del factory de `WindowManager`.
- Esto reinstanciaba `CotizacionDocumentoViewModel` desde cero, perdiendo todo el estado runtime: cliente seleccionado, chip visual del selector, líneas temporales, cantidades, precios, estado dirty, totales calculados, y cualquier cambio no guardado.
- Equivalía arquitectónicamente a abrir el documento de nuevo desde la base de datos, no a moverlo a otro host visual.

**Regla institucional ADR-039 (OBLIGATORIA)**:
> Detach = rehost visual. NO = nuevo documento.  
> El ViewModel y el estado runtime deben sobrevivir el cambio de host visual (inline tab → ventana desacoplada).  
> La ventana desacoplada es un contenedor alternativo para la misma sesión documental, no una sesión nueva.  
> NUNCA guardar automáticamente al desacoplar. NUNCA recargar desde BD. NUNCA recrear el ViewModel.

**Solución adoptada**:
- `BtnAbrirEnVentana_Click` en `CotizacionDocumentoPage` fue refactorizado para rehostear `this` (la misma instancia de página) en el `DetachedDocumentWindow`.
- Flujo correcto de rehost:
  1. `EsInlineMode = false` — oculta controles inline (← Volver, Abrir en ventana) en la página rehosteada.
  2. `VolverALista?.Invoke()` — limpia `DocumentSurfaceContent = null` en el ViewModel del módulo padre, desvinculando sincrónicamente la página del árbol visual inline (requisito WinUI 3: un elemento no puede tener dos padres visuales).
  3. `WindowManager.OpenWindow(factory: () => new DetachedDocumentWindow(paginaActual, titulo))` — rehostea la misma instancia en la nueva ventana.
- El `_sessionKey` (GUID) se genera una sola vez en el constructor de la página para garantizar window key estable en documentos nuevos (sin Id asignado aún).
- El título se construye desde `ViewModel.NombreCliente` (runtime state actual), no desde el snapshot estático `_cotizacionOriginal`.

**Estado runtime preservado**:
- Entidad de Directorio seleccionada (`_entidadDirectorioSeleccionada` en ViewModel)
- Chip visual del `RelacionComercialSelectorControl` (misma instancia de control, no se reconstruye)
- Líneas / detalles en `ViewModel.Detalles` (ObservableCollection en memoria)
- Totales calculados (`Subtotal`, `Total`)
- Estado dirty / `HasChanges`
- Fecha, Observaciones y todos los campos editados
- Estatus del documento

**Anti-patterns PROHIBIDOS (ADR-039)**:
- Crear `new CotizacionDocumentoPage(dto)` dentro de un factory de detach.
- Llamar `ViewModel.Initialize(dto)` al rehostear (equivale a recargar desde BD).
- Guardar automáticamente antes o durante el detach.
- Recargar datos desde la base de datos durante el rehost.
- Perder el estado dirty al cambiar de host visual.
- Instanciar un nuevo ViewModel durante el detach/attach.

**Archivos clave**:
- `Ybridio.WinUI/Views/Ventas/CotizacionDocumentoPage.xaml.cs` → `BtnAbrirEnVentana_Click` refactorizado
- `Ybridio.WinUI/Views/Detached/DetachedDocumentWindow.xaml.cs` → sin cambios (ya acepta cualquier Page)
- `Ybridio.WinUI/Services/Windowing/WindowManager.cs` → sin cambios (ya gestiona lifecycle)

**Validación esperada**:
1. Abrir Cotización inline.
2. Seleccionar cliente (chip aparece).
3. Agregar líneas/productos con cantidades y precios.
4. Pulsar "Abrir en nueva ventana".
5. Resultado: ventana desacoplada muestra exactamente el mismo cliente, chip, líneas, totales y dirty state. Sin auto-save. Sin pérdida de datos.

**Relación con ADRs anteriores**:
- ADR-028: Window Detach Mode — política de límite máximo 2 ventanas desacopladas simultáneas. ADR-039 complementa ADR-028 corrigiendo el lifecycle del contenido.
- ADR-032: Document Surface UX Pattern — inline mode controles. ADR-039 ajusta `EsInlineMode = false` durante el rehost.
- ADR-029: WindowManager como single source of truth para lifecycle de ventanas. ADR-039 usa `WindowManager` correctamente.

---

## ADR-038 — Relación Comercial Bajo Demanda / Directorio como Source of Truth

**Decisión**: `RelacionComercial` es una entidad transaccional operativa, NO un catálogo maestro de UI. El selector institucional consume directamente `Persona` y `EmpresaComercial` (el Directorio). `RelacionComercial` se crea o reutiliza de forma transparente al guardar un documento comercial mediante el patrón `GetOrCreateRelacionComercialAsync`.

**Problema identificado (corrección de ADR-037)**:
- ADR-037 dirigía el selector hacia `RelacionComercial` como fuente de búsqueda.
- Eso requería existencia previa de `RelacionComercial` para que apareciera en el selector.
- Scripts de normalización masiva se proponían para generar `RelacionComercial` preventivamente.
- Esto generaba: relaciones comerciales fantasma, contaminación del dominio, acoplamiento incorrecto, sincronización artificial Directorio↔RelacionComercial, y deuda arquitectónica futura.

**Regla institucional ADR-038 (OBLIGATORIA)**:
> `RelacionComercial` representa un vínculo comercial operativo/transaccional.  
> SOLO debe existir cuando existe interacción comercial real: cotización, pedido, venta, factura.  
> El catálogo maestro del ERP son `core.Persona` y `core.EmpresaComercial`.  
> El selector debe funcionar aunque NO exista `RelacionComercial` previa.

**Solución adoptada**:
- `IDirectorioService.BuscarParaSelectorAsync` — nueva búsqueda directa en `Persona` + `EmpresaComercial`. Usa `IgnoreQueryFilters()` para resolver datos legacy multiempresa correctamente.
- `DirectorioSelectorDto` — nuevo DTO institucional con `EntityType` (Persona/Empresa), `PersonaId`, `EmpresaComercialId`, `DisplayName`, `RFC`, `Email`, `Telefono`, `TipoVisual`, `Glyph`, `InfoSecundaria`.
- `IRelacionComercialService.GetOrCreateAsync` — patrón institucional de materialización bajo demanda: reutiliza relación existente si ya existe; crea automáticamente si no.
- `RelacionComercialSelectorControl` — refactorizado para usar `IDirectorioService` / `DirectorioSelectorDto`. El evento `SelectionChanged` emite `DirectorioSelectorDto?`.
- Los 4 ViewModels comerciales (`CotizacionDocumentoViewModel`, `PedidoDocumentoViewModel`, `VentaDocumentoViewModel`, `OrdenTrabajoDocumentoViewModel`) almacenan `DirectorioSelectorDto?` y resuelven `RelacionComercialId` en `GuardarAsync` vía `GetOrCreateAsync`.

**Flujo correcto (ADR-038)**:
1. Usuario busca en el selector → `IDirectorioService.BuscarParaSelectorAsync` → retorna `Persona` y `EmpresaComercial` directamente.
2. Usuario selecciona entidad → ViewModel guarda `DirectorioSelectorDto?`, `NombreCliente`.
3. Al guardar → `GetOrCreateAsync` → reutiliza o crea `RelacionComercial` → obtiene `RelacionComercialId`.
4. Documento se persiste con `RelacionComercialId` ya resuelto.

**Anti-patterns PROHIBIDOS (ADR-038)**:
- Usar `RelacionComercial` como catálogo UI o fuente de búsqueda para selectores.
- Scripts de normalización masiva que generen `RelacionComercial` preventivamente para aparecer en UI.
- Sincronización artificial Directorio ↔ `RelacionComercial`.
- Exigir existencia previa de `RelacionComercial` para que una entidad aparezca en el selector.
- Crear `RelacionComercial` vacías/fantasma solo para alimentar UI.

**Archivos clave**:
- `Ybridio.Application/DTOs/Directorio/DirectorioDto.cs` → `DirectorioSelectorDto`, `DirectorioEntityType`
- `Ybridio.Application/Services/Directorio/IDirectorioService.cs`
- `Ybridio.Application/Services/Directorio/DirectorioService.cs`
- `Ybridio.Application/Services/Directorio/IRelacionComercialService.cs` → `GetOrCreateAsync`
- `Ybridio.Application/Services/Directorio/RelacionComercialService.cs` → `GetOrCreateAsync`
- `Ybridio.WinUI/Controls/Selector/RelacionComercialSelectorControl.xaml[.cs]` → migrado a `DirectorioSelectorDto`

**Relación con ADR-037**: ADR-037 sigue vigente para el patrón de control reusable y UX (debounce, keyboard, preview, badges). ADR-038 corrige el source of truth y elimina la dependencia del selector hacia `RelacionComercial`.

---

## ADR-037 — RelacionComercial Entity Selector Pattern (Selector Institucional)

**Decisión**: Estandarizar la selección de socios comerciales (cliente, empresa, persona, prospecto) en todo el ERP mediante el control reusable `RelacionComercialSelectorControl` (WinUI UserControl), respaldado por `IRelacionComercialService.ListarParaSelectorAsync`.

**Problema identificado**:
- Cotizaciones usaban `AutoSuggestBox` ad hoc con lógica de búsqueda duplicada en el ViewModel.
- Pedidos y Ventas usaban `TextBox` libre sin búsqueda ni validación.
- Órdenes de Trabajo idem con texto libre.
- Sin query institucional unificado: cada superficie implementaba su propio patrón de búsqueda parcial o sin búsqueda.
- El método `ListarParaSelectorAsync` usaba `Include()` con QueryFilters globales activos, lo que causaba que `EmpresaComercial` y `Persona` se resolvieran como `null` cuando su `EmpresaId` no coincidía exactamente con `_session.EmpresaId` (datos legacy / multiempresa). Resultado: el selector no retornaba resultados aunque existieran registros en BD.
- Datos huérfanos: `EmpresaComercial` y `Persona` creadas antes de ADR-036 sin `RelacionComercial` correspondiente.

**Solución adoptada**:
- `RelacionComercialSelectorControl` — control WinUI reusable en `Ybridio.WinUI/Controls/Selector/`. Implementa búsqueda incremental con debounce (250ms), cancellation safety (ADR-026), keyboard UX (↑↓/Enter/Esc), preview de entidad seleccionada, badges semánticos por tipo.
- `ListarParaSelectorAsync` reescrito con **proyección LINQ directa + join explícito** usando `IgnoreQueryFilters()` en `Personas` y `EmpresasComerciales`. Esto evita que el QueryFilter de empresa filtre las navegaciones y resuelva `null`. El filtro de empresa sigue aplicándose en `RelacionComercial` (tabla raíz).
- Búsqueda incremental en: `Nombre`, `Apellidos`, `RazonSocial`, `NombreComercial`, `RFC`, `Email` de ambas entidades.
- Scripts de diagnóstico y normalización en `Documentation/Scripts/`.
- `EntitySelectorStyles.xaml` + merge en `Styles.xaml` como source of truth visual.

**Regla institucional** (obligatoria desde ADR-037):
> Todo flujo ERP que requiera seleccionar Cliente, Empresa, Persona, Prospecto o RelacionComercial **DEBE** usar `RelacionComercialSelectorControl`. Prohibido: TextBox libre, ComboBox gigante, AutoSuggestBox ad hoc.

**Anti-patterns prohibidos**:
- `IRelacionComercialService` inyectado directamente en ViewModels para búsqueda de socios → usar el control.
- `Include(r => r.EmpresaComercial).Include(r => r.Persona)` sin `IgnoreQueryFilters()` en el selector → resuelve null con datos legacy.
- Búsqueda por exact-match o solo nombre → usar contains en nombre + RFC + email.
- `EmpresaId = 0` pasado al control → siempre vincular a `_session.EmpresaId`.

**Superficies migradas**:
- `CotizacionDocumentoPage` ✅
- `PedidoDocumentoPage` ✅
- `VentaDocumentoPage` ✅
- `OrdenTrabajoDocumentoPage` ✅

**Scripts de datos**:
- `Documentation/Scripts/diagnostico_relacion_comercial.sql` — detecta huérfanos e invariantes violadas.
- `Documentation/Scripts/normalizacion_relacion_comercial.sql` — genera `RelacionComercial` faltantes (EmpresaComercial→Cliente, Persona→Prospecto).

**Archivos clave**:
- `Ybridio.WinUI/Controls/Selector/RelacionComercialSelectorControl.xaml[.cs]`
- `Ybridio.WinUI/Styles/Selector/EntitySelectorStyles.xaml`
- `Ybridio.Application/Services/Directorio/RelacionComercialService.cs` → `ListarParaSelectorAsync`
- `Ybridio.Application/DTOs/Directorio/RelacionComercialDto.cs` → `RelacionComercialSelectorDto`

---

## ADR-036 — Business Partner Model: Cliente → RelacionComercial + Directorio

**Decisión**: Reemplazar la entidad `Cliente` de semántica mixta por un modelo de socio comercial basado en tres entidades del dominio: `Persona` (directorio de personas físicas), `EmpresaComercial` (directorio de empresas externas), y `RelacionComercial` (rol comercial del socio frente a la empresa tenant).

**Problema identificado**:
- `Cliente` mezclaba identidad directorial (RFC, contacto, nombre) con rol comercial (tipo, crédito, estado).
- Un mismo tercero podía ser cliente y proveedor, requiriendo duplicidad de registros.
- No existía distinción entre persona física y empresa en el modelo de dominio.
- El módulo "Contactos" era un placeholder sin arquitectura definida.

**Solución adoptada**:
- `Persona` → entidad en `core.Persona` para personas físicas/contactos, con FK opcional a `EmpresaComercial`.
- `EmpresaComercial` → entidad en `core.EmpresaComercial` para empresas externas (no tenant), con RFC y datos fiscales.
- `RelacionComercial` → entidad de vínculo en `core.RelacionComercial` con `TipoRelacionComercial` (`Prospecto`, `Cliente`, `Proveedor`, `Mixto`), `LimiteCredito`, `Activo` y FK exclusiva a Persona XOR EmpresaComercial.
- `TipoRelacionComercial` enum en `Ybridio.Domain.Catalogos`.
- Todos los documentos de venta (`Cotizacion`, `Pedido`, `OrdenTrabajo`, `Venta`, `Factura`) usan `RelacionComercialId` como FK con `OnDelete(SetNull)`.
- `NombreCliente` se mantiene como campo denormalizado en documentos para integridad histórica.
- Módulo Shell `"Contactos"` renombrado a `"Directorio"`.

**Alternativas descartadas**:
- Mantener `Cliente` y agregar tabla `Proveedor` separada → duplicidad de campos de identidad.
- Usar tabla de roles polimórfica → complejidad EF innecesaria para este scope.
- Usar `Cliente` con flag `EsProveedor` → antipatrón de campo booleano de rol.

**Impacto en capas**:
- **Domain**: `Ybridio.Domain.Catalogos` — nuevas entidades + enum. Documentos de venta actualizados.
- **Infrastructure**: Configuraciones EF en `Configurations/Catalogos/`. `ErpDbContext` extendido. Migración `AddBusinessPartnerModel` generada.
- **Application**: `DTOs/Directorio/` + `Services/Directorio/`. `PermisosClave.Directorio` añadido. DTOs de venta actualizados.
- **WinUI**: `CotizacionDocumentoViewModel` usa `IRelacionComercialService` y `RelacionComercialSelectorDto`. Shell routa `"Directorio"`.

**Compatibilidad**: `DbSet<Cliente> Clientes` permanece en `ErpDbContext` para compatibilidad de migración. `PermisosClave.Cliente.*` preservados. `ClienteService` y páginas `Clientes*` permanecen operativas en paralelo hasta migración completa de UX.

---

## ADR-035 — Column Density System + Financial Formatting Semantics

**Decisión**: Extender el Operational Grid Standard (ADR-032) con un sistema institucional de distribución de columnas por prioridad operacional y con un estándar semántico de formateo financiero.

**Problema identificado**:
- Columnas dimensionadas arbitrariamente (`Width="150"`, `Width="200"`) generaban desiertos visuales o distribuciones sin lógica operacional.
- Columnas principales (Cliente, Nombre) no expandían para ocupar el espacio horizontal útil.
- Valores financieros mostrados como `3337.00` sin símbolo monetario — pérdida de claridad semántica.
- Sin regla explícita: cada pantalla re-inventaba la distribución de columnas.

**Operational Column Density System — tipos oficiales**:

| Tipo | Usar | Ancho | Ejemplos |
|---|---|---|---|
| PRIMARY EXPANDABLE | `Width="*"` o `Width="2*"` | Proporcional, consume espacio sobrante | Cliente, Nombre, Producto, Descripción |
| COMPACT SEMANTIC | `OgColCompact` (90), `OgColDate` (100), `OgColStatus` (110) | Fijo mínimo semántico | Folio, Fecha, Estado, Tipo |
| FINANCIAL COMPACT | `OgColFinancial` (130) | Fijo financiero institucional | Total, Precio, Costo, LímiteCredito |
| GUTTER | `OgColGutter` (8) | Fijo — respiro izquierdo | Primera columna siempre |

**Financial Formatting Standard**:
- Todo valor financiero visible incluye símbolo monetario: `$3,337.00`, `$347,746.00`.
- Alineación obligatoria: derecha.
- Usar `DecimalToCurrencyConverter` en el binding — no formatear en ViewModel ni en code-behind.
- Estilo obligatorio: `OgCurrencyTextStyle` (alias de `OgCellFinancialStyle` — SemiBold, Right, 13px).
- Cultura de formateo: `es-MX` (símbolo `$`, separador de miles `,`, decimal `.`).

**Tokens definidos en `Styles/Grid/OperationalGridBase.xaml`**:
```xml
<GridLength x:Key="OgColGutter">8</GridLength>
<GridLength x:Key="OgColCompact">90</GridLength>
<GridLength x:Key="OgColDate">100</GridLength>
<GridLength x:Key="OgColStatus">110</GridLength>
<GridLength x:Key="OgColFinancial">130</GridLength>
```

**Converters**:
- `DecimalToCurrencyConverter` — en `Ybridio.WinUI/Converters/DecimalToCurrencyConverter.cs`.

**Páginas piloto aplicadas**:
- `CotizacionesPage.xaml` — Cliente expandible (`Width="*"`), Total con `CurrencyConverter`.
- `ClientesPage.xaml` — Nombre expandible (`Width="2*"`), Email expandible (`Width="*"`), LimiteCredito con `CurrencyConverter`.

**Alternativa descartada**: Anchos fijos arbitrarios por pantalla — descartado por desiertos visuales y falta de coherencia operacional entre módulos.

**Regla institucional**:
- PROHIBIDO `Width="150"`, `Width="200"` u otros anchos mágicos arbitrarios.
- La distribución surge del sistema operacional: tokens `OgCol*` o columnas star (`*`, `2*`).
- TODO valor financiero usa `DecimalToCurrencyConverter` + `OgCurrencyTextStyle`.
- PROHIBIDO formatear moneda en el ViewModel o en code-behind.

---

## ADR-034 — Pixel Perfect Operational Layout System

**Decisión**: Institucionalizar un sistema de layout, spacing y alineación visual operacional para todo el ERP. Spacing scale oficial, content boundary system (20px lateral), CommandBars full-width alineadas, y Document Surface que reemplaza la región operacional completa.

**Problema identificado**:
- CommandBars con `HorizontalAlignment="Left" Margin="8"` creaban desalineación visual con contenido (boundary 20px).
- Filter rows con `Padding="8,8,20,4"` — inconsistente con el contenido lateral.
- Status bars visibles cuando Document Surface estaba abierto, generando ruido visual.
- Márgenes arbitrarios (`Margin="13"`, `Padding="7"`) en distintos módulos rompían el ritmo visual.
- Sin tokens reutilizables: cada pantalla re-inventaba su propio spacing.

**Estructura oficial adoptada**:
```
Styles/
  Layout/LayoutBase.xaml          ← Spacing tokens + content boundary + container styles
  CommandBars/CommandBarsBase.xaml ← ErpModuleCommandBarStyle + ErpDocumentCommandBarStyle
```

**Spacing scale oficial**:
- `ErpSpace4` = 4px, `ErpSpace8` = 8px, `ErpSpace12` = 12px
- `ErpSpace16` = 16px, `ErpSpace24` = 24px, `ErpSpace32` = 32px

**Content boundary system**:
- `ErpContentBoundary` = 20px (valor numérico)
- `ErpContentBoundaryThickness` = `Thickness(20,0,20,0)` — margin horizontal estándar
- `ErpContentBoundaryWithTopGap` = `Thickness(20,8,20,0)` — cards/grids con gap superior
- `ErpCommandBarPadding` = `Thickness(6,0,0,0)` — alinea primer botón con 20px visual boundary
- `ErpFilterRegionPadding` = `Thickness(20,8,20,4)` — row de búsqueda/filtros
- `ErpStatusRegionMargin` = `Thickness(20,0,20,8)` — status bar

**Container styles**:
- `ErpOperationalCardStyle` — card blanco con border #E5E5E5, margin con top gap
- `ErpOperationalCardTopStyle` — variante sin top gap (primeros cards tras CommandBar)
- `ErpTotalesCardStyle` — card de totales con padding interno estándar

**CommandBar styles**:
- `ErpModuleCommandBarStyle` — módulos CRUD/listado: Stretch + alineación 20px
- `ErpDocumentCommandBarStyle` — Document Surfaces y ventanas standalone

**Páginas de referencia actualizadas** (ADR-034 aplicado):
- `CotizacionesPage.xaml` — CommandBar, filter row, status bar visibility
- `CotizacionDocumentoPage.xaml` — CommandBar, todos los containers, header, totales, status

**Regla Document Surface**:
- Cuando `IsDocumentSurfaceVisible=true`, la status bar del listado se oculta automáticamente.
- El Document Surface reemplaza visualmente la región operacional completa.

**Alternativa descartada**: Mantener márgenes hardcoded por pantalla — descartado por imposibilidad de mantenimiento y UX inconsistente entre módulos.

**Regla institucional**:
- PROHIBIDO márgenes arbitrarios (`Margin="7"`, `Margin="13"`).
- TODO spacing proviene de tokens `Erp*` definidos en `Styles/Layout/LayoutBase.xaml`.
- TODO alignment de CommandBar usa `ErpModuleCommandBarStyle` o `ErpDocumentCommandBarStyle`.

---

## ADR-033 — Styles/ como Source of Truth Visual (Visual Design System)

**Decisión**: Establecer `Styles/` como la fuente de verdad visual oficial del ERP. `App.xaml` queda limitado a bootstrap/merge. Ningún estilo nuevo se agrega directamente en `App.xaml`.

**Problema identificado**:
- Estilos rápidos comenzaron a acumularse en `App.xaml` durante la evolución UX de ADR-031/032.
- Aparecieron nombres ambiguos (`TransparentButtonStyle`), duplicaciones y referencias rotas (`XamlParseException`).
- `App.xaml` crecía orgánicamente sin estructura ni semántica clara, dificultando mantenimiento a largo plazo y colaboración IA/dev.

**Estructura oficial adoptada**:
```
Styles/
  Styles.xaml              ← Dictionary maestro (único punto de entrada)
  Buttons/ButtonsBase.xaml ← Botones e interacciones
  DataGrid/DataGridBase.xaml ← Listas, grids, tablas
  Forms/FormBase.xaml      ← Formularios CRUD
  Tabs/TabsBase.xaml       ← Navegación por tabs (Module + Workspace layers)
```

**Estilos migrados de App.xaml**:
- `DocumentSurfaceBackActionButtonStyle` → `Styles/Buttons/ButtonsBase.xaml`
- `OutlookTabItemStyle` → `Styles/Tabs/TabsBase.xaml`
- `WorkspaceTabItemStyle` → `Styles/Tabs/TabsBase.xaml`

**Reglas institucionales**:
1. `Styles/Styles.xaml` es el único dictionary que `App.xaml` mergea (además de `XamlControlsResources`).
2. Todo nuevo estilo reutilizable va en el subdirectorio semántico correspondiente de `Styles/`.
3. Naming obligatoriamente semántico (expresa intención UX, no apariencia visual).
4. PROHIBIDO: estilos inline (`<Button.Style>...</Button.Style>`), nombres ambiguos, duplicación visual.
5. Para añadir un nuevo dominio: crear `Styles/<Dominio>/<Dominio>Base.xaml` y registrar en `Styles/Styles.xaml`.

**Build**: ✅ 0 errores de compilación.

---

## ADR-032 — Document Surface + Window Mode: Patrón Institucional Oficial

**Decisión**: Simplificar y formalizar el patrón de apertura de documentos como el estándar institucional para todos los módulos CRUD/documentales del ERP. El patrón define exactamente dos modos posibles:

1. **INLINE contextual** — el documento reemplaza el grid dentro del módulo (content replacement). El usuario permanece en el contexto del módulo. Se muestran "Volver a Lista" y "Abrir en nueva ventana".
2. **WINDOW standalone** — el documento se abre en una ventana OS real independiente usando `WindowManager`. El usuario sale del contexto del módulo para multitarea real. No se muestran controles inline.

**Problema identificado**:
- ADR-027 (Detachable Mode) introducía un tercer estado: split view grid + surface simultáneo. Esto incrementaba la complejidad visual y del lifecycle sin beneficio real para el usuario PYME.
- Los callbacks `ToggleDetach`, `IsDocumentSurfaceDetached`, y la lógica de mutación de columnas en `AjustarLayoutDetached` resultaban en código frágil y difícil de entender.
- El menú contextual secundario con "Desacoplar Surface" era confuso y raramente utilizable.
- El split view reducía el espacio útil de ambos paneles simultáneamente.

**Alternativas descartadas**:
- **Mantener Detach Mode (ADR-027)**: compleja lógica de columnas, tres estados ambiguos, anti-pattern para ERP desktop-native.
- **Split View persistente**: rompe la claridad operacional; el usuario no sabe si está "en el módulo" o "en el documento".
- **ContentDialog/Flyout para documentos**: bloquea UI, espacio insuficiente para formularios con líneas de detalle.

**Razón**: El patrón "abrir aquí" vs "abrir en ventana" es intuitivo, desktop-native, y cubre el 100% de los casos de uso reales:
- **Apertura rápida contextual**: doble clic en grid → documento inline → trabajar → volver.
- **Multitarea real**: inline → "Abrir en nueva ventana" → ventana OS independiente → módulo regresa al grid.
- **Sin estados ambiguos**: solo dos modos, nunca simultáneos en el mismo módulo.
- **WindowManager preservado**: la ventana standalone sigue usando `IWindowManager` con key `detached:`, respetando el límite máximo de 2 ventanas concurrentes (ADR-028/ADR-029).

**Implementación en Cotizaciones (módulo piloto)**:
- `CotizacionesViewModel`: eliminado `IsDocumentSurfaceDetached`, `ToggleDetach`, `ToggleDetachCommand`. Solo `IsDocumentSurfaceVisible` y `DocumentSurfaceContent`.
- `CotizacionesPage.xaml`: eliminado layout de 3 columnas; grid simple de content replacement.
- `CotizacionesPage.xaml.cs`: eliminados `ViewModel_PropertyChanged`, `AjustarLayoutDetached`, `OnToggleDetach`. Añadido `EsInlineMode = true` al wiring del DocumentPage.
- `CotizacionDocumentoPage.xaml`: eliminado `BtnToggleDetach`. `BtnVolverALista` y `BtnAbrirEnVentana` con `Visibility="Collapsed"` por defecto; activados via `EsInlineMode`.
- `CotizacionDocumentoPage.xaml.cs`: propiedad `EsInlineMode` controla visibilidad de controles inline; `BtnAbrirEnVentana_Click` abre ventana OS y llama `VolverALista?.Invoke()` para cerrar el inline automáticamente.

**Estándar para futuros módulos**:
- `[Módulo]ViewModel`: solo `IsDocumentSurfaceVisible` + `DocumentSurfaceContent` + `CerrarDocumentSurfaceAsync()`.
- `[Módulo]Page.xaml.cs`: wiring `page.EsInlineMode = true` al abrir inline.
- `[Documento]Page.xaml.cs`: propiedad `EsInlineMode`, `VolverALista`, `BtnAbrirEnVentana_Click` usando `IWindowManager`.
- NO usar: `IsDocumentSurfaceDetached`, split layouts, `ToggleDetach`, callbacks detach, 3+ estados visuales.

**Build**: ✅ 0 errores de compilación.

---

## ADR-031 — Document Surface Visual Separation Standard

**Decisión**: Formalizar oficialmente la **separación visual y funcional** entre navegación de módulo (tabs de módulo) y documento activo (Document Surface). Eliminar definitivamente el anti-pattern de CRUDs simples abiertos como workspace tabs. Los documentos de Pedidos, Órdenes de Trabajo y Ventas Documentales se abren ahora como **Inline Document Surface** (content replacement), no como tabs de workspace. Se define la jerarquía UX obligatoria: `Tabs módulo → Document Surface Header → Toolbar → Contenido`.

**Problema identificado**:
- **Tabs documentales ensimados**: Pedidos, OT y Ventas Documentales abrían documentos CRUD simples como tabs en `IWorkspaceService`, creando una doble capa de tabs visual: tabs de módulo + tabs documentales debajo.
- **Apariencia browser/IDE**: El ERP se percibía como Chrome o Visual Studio con tabs infinitos en lugar de una aplicación ERP desktop-native PYME.
- **Jerarquía UX confusa**: El usuario no distinguía inmediatamente entre "navegación de módulo" y "documento activo".
- **Acumulación de tabs triviales**: WorkspaceService acumulaba tabs CRUD simples que saturaban el workspace mezclados con workflows reales.
- **Transparencia y overlay**: Las superficies documentales con fondo translúcido encima de tabs de módulo generaban ensimamiento visual confuso.

**Alternativas consideradas**:
- **Mantener tabs workspace para CRUDs simples**: simple pero perpetúa la jerarquía UX incorrecta y el look browser/IDE.
- **NavigationView secundario dentro del módulo**: complejidad innecesaria; no resuelve la apariencia de tab.
- **Dialog modal para formularios**: rompe el flujo operacional PYME; los formularios de pedido tienen líneas y detalles, no caben en un dialog.
- **ContentDialog con formulario completo**: mismo problema; además limita el espacio disponible y bloquea interacción.

**Razón**: Document Surface Visual Separation Standard (ADR-031) es:
- **Corrección de jerarquía UX**: tabs de módulo = navegación; Document Surface = contenido operacional activo. Son niveles distintos que nunca deben verse iguales.
- **Desktop-native ERP**: Outlook-style, no browser-style. El usuario opera en formularios limpios que reemplazan el listado temporalmente (content replacement inline).
- **Anti-acumulación**: el workspace queda limpio; solo contiene workflows complejos reales (OT multi-paso, análisis, multi-documento).
- **Header operacional**: breadcrumb ligero `Módulo › Título`, badge estado, botón volver — sin línea azul activa, sin close ×, sin apariencia tab.
- **Patrones explícitos**: `IsDocumentSurfaceVisible`, `DocumentSurfaceContent`, `ContentPresenter`, callback `OnCerrar` — codificados uniformemente en todos los módulos migrados.
- **IWorkspaceService preservado**: no se rehizo; sigue siendo la autoridad para workflows complejos y navegación cruzada entre documentos relacionados.

**Documentación completa**: Ver `Documentation/ADR-031-Document-Surface-Visual-Separation-Standard.md`

**Impacto técnico**:
- `PedidosPage`, `PedidoDocumentoPage`, `PedidosViewModel`: migrados a inline Document Surface; eliminado `_workspace.OpenTab`.
- `OrdenesTrabajoPage`, `OrdenTrabajoDocumentoPage`, `OrdenesTrabajoViewModel`: mismo patrón.
- `VentasDocumentalesPage`, `VentaDocumentoPage`, `VentasDocumentalesViewModel`: mismo patrón.
- Cada Document Page tiene propiedad `OnCerrar: Func<Task>?` y handler `BtnVolver_Click`.
- Cada Host ViewModel tiene `IsDocumentSurfaceVisible`, `DocumentSurfaceContent`, `IsDocumentSurfaceDetached`, `CerrarDocumentSurfaceAsync()`.
- Cada Host XAML usa `InverseBoolToVisibilityConverter` para ocultar listado y `ContentPresenter` para mostrar surface.
- Build: ✅ 0 errores.
- Runtime Observability: intacta.
- WorkspaceService: preservado sin cambios.

---

## ADR-029 — Window Management Standards: Centralized Runtime Authority

**Decisión**: Formalizar **`WindowManager`** como **single source of truth OBLIGATORIO** para TODO window lifecycle management en el ERP. Toda ventana runtime (detached, dialog, detail, wizard, secondary) debe crearse vía `WindowManager.OpenWindow<TWindow, TKey>(...)`, obedecer policies globales centralizadas (ej: máximo 2 detached windows), y lifecycle automático (ownership Win32, tracking, cleanup). **Prohibición explícita** de window management manual disperso (`new Window()` fuera del manager), services paralelos (`IDetachedWindowManager` eliminado), y tracking duplicado.

**Problema identificado**:
- **Window Detach Mode (ADR-028) inicial creó servicio paralelo** `IDetachedWindowManager`/`DetachedWindowManager` que duplicaba funcionalidad de `WindowManager` existente
- **Tracking duplicado** de ventanas activas (`_windows` en WindowManager + `_activeWindows` en DetachedWindowManager)
- **Lifecycle management inconsistente**: ownership Win32, cleanup handlers, z-order, focus dispersos entre managers
- **Policy enforcement fragmentado**: límite máximo 2 ventanas detached vivía en manager secundario, NO centralizado
- **Riesgo de leaks** por cleanup disperso y handlers duplicados
- **Violación DRY**: lógica `new Window()`, `AppWindow.GetFromWindowId(...)`, `Resize`, `Activate`, `Closed += ...` duplicada en múltiples archivos

**Alternativas consideradas**:
- **Mantener `IDetachedWindowManager` paralelo**: simple pero viola single source of truth, fragmenta policy enforcement, duplica código
- **Crear múltiples managers por categoría** (`IDialogManager`, `IWizardManager`, etc.): complejidad innecesaria, DI explosion, tracking paralelo
- **Window management manual en Pages/ViewModels**: caos disperso, NO centralización, imposible observabilidad runtime
- **Agregar `WindowCategory` enum explícito**: más verboso que convention key prefix; rompe API existente

**Razón**: Window Management Standards (ADR-029) es:
- **Single source of truth**: `WindowManager` es autoridad runtime única; TODO lifecycle ventanas centralizado
- **Policy enforcement consistente**: límites globales (ej: máximo 2 detached) aplicados uniformemente vía convention key prefix `"detached:"`
- **DRY eliminado**: NO duplicación window creation/ownership/tracking/cleanup; una línea lógica UI (`_windowManager.OpenWindow(...)`)
- **Extensible por convention**: prefijos key (`"detached:"`, `"dialog:"`, `"wizard:"`) permiten policies futuras sin romper API
- **Observabilidad centralizada**: logging diagnóstico único `[WindowManager]`, tracking `_detachedWindowsCount` para Runtime Diagnostic Panel futuro
- **Exception tipada**: `DetachedWindowLimitException` para manejo claro en UI (try/catch + ContentDialog)
- **Limpieza arquitectónica**: eliminados `IDetachedWindowManager`, `DetachedWindowManager`, DI registration secundaria; consolidado bajo `WindowManager` existente

**Documentación completa**: Ver `Documentation/ADR-029-Window-Management-Standards.md`

**Impacto técnico**:
- Extensión `WindowManager`: contador `_detachedWindowsCount`, constantes `MaxDetachedWindows=2` y `DetachedKeyPrefix="detached:"`, validación límite en `OpenWindow` con `DetachedWindowLimitException`, incremento/decremento automático handlers
- Nueva excepción: `DetachedWindowLimitException` operacional para UI
- Window helper: `DetachedDocumentWindow` code-only (sin XAML) en `Ybridio.WinUI/Views/Detached/`
- `CotizacionDocumentoPage`: migrado de `IDetachedWindowManager` a `IWindowManager`, key `"detached:cotizacion:{id}"`, try/catch exception
- Eliminados: `IDetachedWindowManager.cs`, `DetachedWindowManager.cs`, DI registration secundaria
- Build: ✅ 0 errores
- Runtime validation pendiente por usuario

---

## ADR-028 — Document Surface Window Detach Mode: Real Desktop Multitasking Extension

**Decisión**: Evolucionar el **Document Surface UX Pattern (ADR-025 + ADR-027)** hacia **tres modos oficiales** con propósitos UX específicos: (1) **Inline content replacement** (default), (2) **Quick Preview Split Surface** (read-only lateral ligero — postponed), (3) **Window Detach Mode** (ventana OS real independiente — implementado piloto Cotizaciones).

**Problema identificado**:
- **Split view actual (ADR-027) es binario**: desacoplado ON/OFF, pero ambos modos muestran el documento completo editable
- **NO existe modo ligero de consulta rápida** (quick preview read-only) que muestre solo información esencial sin formularios pesados
- **NO existe modo ventana OS real independiente** para multitarea desktop nativa (comparar, copiar, multi-monitor)
- **Pantallas pequeñas**: Split view horizontal ocupa mucho espacio; en monitores 15" el split se siente apretado
- **Multitarea desktop real**: Usuarios multi-monitor necesitan verdadera independencia de ventanas OS, no solo split dentro de la misma ventana principal

**Alternativas consideradas**:
- **Mantener solo split view editable (ADR-027)**: no resuelve pantallas pequeñas ni multitarea desktop real
- **Volver a Workspace Tabs infinitos**: reintroduce caos UX, pérdida contexto módulo, fragmentación (anti-pattern institucionalizado)
- **Floating windows ilimitadas**: complejidad UX excesiva, leaks, DbContext collisions masivas, lifecycle management complejo
- **Dock manager enterprise**: complejidad arquitectónica no justificada, no cumple principio mínima intervención
- **Quick Preview modal**: rompe flujo contextual, no permite mantener grid visible

**Razón**: Document Surface Window Detach Mode es:
- **Extensión controlada no invasiva**: preserva completamente WorkspaceService, Shell, Runtime Observability, Security Foundation, arquitectura actual
- **Tres modos UX con propósitos específicos**:
  1. **Inline** (default): Content replacement, edición principal PYME
  2. **Quick Preview** (postponed): Split lateral read-only ligero, consulta rápida contextual
  3. **Window Detach** (implementado): Ventana OS real, multitarea desktop, multi-monitor, pantallas pequeñas
- **Límite máximo 2 ventanas desacopladas**: evita caos UX, leaks, DbContext collisions masivas, lifecycle management complejo
- **Mensaje operacional claro**: *"Límite alcanzado: máximo 2 ventanas desacopladas simultáneas. Cierre alguna ventana antes de abrir otra."*
- **Cleanup automático**: `window.Closed += ...` libera slot al cerrar
- **State independiente**: Cada ventana tiene su propia instancia página/ViewModel; NO comparten state mutable
- **Piloto acotado**: implementación inicial SOLO en Cotizaciones, validación UX y estabilidad antes de expandir

**Documentación completa**: Ver `Documentation/ADR-028-Document-Surface-Window-Detach-Mode.md`

**Impacto técnico**:
- Nuevo servicio: `IDetachedWindowManager` / `DetachedWindowManager` (WinUI 3 AppWindow)
- DI registration: `services.AddSingleton<IDetachedWindowManager, DetachedWindowManager>();`
- Botón secundario "Abrir en Ventana" en `CotizacionDocumentoPage.xaml`
- Handler `BtnAbrirEnVentana_Click` en code-behind con snapshot `_cotizacionOriginal`
- Build: ✅ 0 errores
- Runtime validation pendiente por usuario

---

## ADR-027 — Document Surface Detachable Mode: Controlled Lightweight Multitasking Extension

**Decisión**: Extender el **Document Surface UX Pattern (ADR-025)** con un **modo desacoplado opcional** que permite visualización simultánea del grid de listado y el Document Surface en split view side-by-side, para soportar escenarios operacionales de multitarea ligera (comparar información, copiar texto, consultar lista mientras edita) sin regresar al modelo anterior de Workspace Tabs infinitos para CRUDs simples.

**Problema identificado**:
- **Document Surface UX Pattern funciona correctamente**: content replacement layout (grid XOR surface) es simple, limpio, minimalista, PYME-friendly para operación diaria rápida
- **PERO existen escenarios operacionales válidos**: usuario necesita ocasionalmente comparar cotizaciones, copiar datos entre documentos, revisar grid mientras edita, consultar lista sin cerrar el documento activo
- **Limitación del content replacement puro**: cuando el usuario abre una cotización para editar, pierde completamente visibilidad del grid de listado
- **NO queremos volver a Workspace Tabs globales**: tabs persistentes para CRUDs simples creaban caos UX, acumulación innecesaria, navegación fragmentada
- **Necesidad de multitarea ligera controlada**: permitir operación paralela grid+documento SOLO cuando el usuario lo solicita explícitamente, manteniendo simplicidad por defecto

**Alternativas consideradas**:
- **Mantener solo content replacement**: simple pero no resuelve escenarios operacionales de comparación/consulta simultánea
- **Volver a Workspace Tabs persistentes para CRUDs**: solucionaría multitarea pero reintroduce caos de tabs infinitos, pérdida de contexto módulo, fragmentación UX
- **Split view permanente por defecto**: ocupa mucho espacio, ruido visual constante, no apto para PYME operación diaria minimalista
- **Múltiples surfaces desacopladas simultáneas**: complejidad UX excesiva, caos visual, problemas de DbContext concurrency, lifecycle management complejo
- **Floating windows OS reales**: fuera de scope para ERP PYME, overhead innecesario, rompe estilo Outlook 2026 limpio
- **Dock manager enterprise**: complejidad arquitectónica no justificada, no cumple principio de mínima intervención

**Razón**: Document Surface Detachable Mode es:
- **Extensión controlada no invasiva**: preserva completamente WorkspaceService, Shell, Runtime Observability, Security Foundation, arquitectura actual; solo agrega nueva capacidad UX opcional en capa presentación
- **UX simple por defecto**: modo normal sigue siendo content replacement (grid XOR surface), comportamiento existente preservado al 100%
- **Multitarea ligera bajo demanda**: usuario activa desacoplamiento explícitamente mediante botón discreto "Desacoplar Surface" en CommandBar secundario del documento
- **Limitación de complejidad**: SOLO permitir 1 Document Surface desacoplada activa por módulo, evitando caos visual y problemas de concurrency runtime
- **Split view limpio**: layout grid columnas 2*/3* con separador visual, NO floating windows, NO dock managers, mantiene estética Outlook 2026 minimalista
- **State preservation garantizado**: filtros, selección, scroll, contexto módulo se mantienen durante acoplar/desacoplar
- **Runtime Observability compatible**: integración completa con Runtime Diagnostic Panel, sin overhead adicional
- **Piloto acotado**: implementación inicial SOLO en Cotizaciones, validación UX y estabilidad antes de expandir a otros módulos
- **Workspace Tabs siguen reservados**: workflows largos, multi-documento complejo, OT complejas, análisis persistente continúan usando tabs workspace

**Impacto**:

### CotizacionesViewModel.cs (ViewModel del módulo piloto)

```csharp
// ── Document Surface Detachable Mode (ADR-027) ──────────────────────────
/// <summary>
/// Indica si el Document Surface está en modo desacoplado (detached).
/// false (default) = Content Replacement (grid XOR surface)
/// true = Detached Mode (grid + surface simultáneos side-by-side)
/// Permite multitarea ligera controlada sin volver a Workspace Tabs infinitos.
/// </summary>
[ObservableProperty] private bool isDocumentSurfaceDetached;

public void AbrirNuevaCotizacion()
{
    DocumentSurfaceContent = null;
    IsDocumentSurfaceVisible = true;
    IsDocumentSurfaceDetached = false; // Default: content replacement mode
}

public void AbrirEditarCotizacion(CotizacionDto cotizacion)
{
    DocumentSurfaceContent = cotizacion;
    IsDocumentSurfaceVisible = true;
    IsDocumentSurfaceDetached = false; // Default: content replacement mode
}

public async Task CerrarDocumentSurfaceAsync()
{
    IsDocumentSurfaceVisible = false;
    IsDocumentSurfaceDetached = false; // Reset detached state
    DocumentSurfaceContent = null;
    await RefrescarAsync(); // Refrescar grid después de cerrar
}

/// <summary>
/// Alterna entre modo acoplado (content replacement) y modo desacoplado (split view).
/// Modo acoplado: grid XOR surface
/// Modo desacoplado: grid + surface simultáneos side-by-side (multitarea ligera)
/// </summary>
[RelayCommand]
public void ToggleDetach()
{
    if (!IsDocumentSurfaceVisible) return; // Solo permitir detach cuando surface está activo
    IsDocumentSurfaceDetached = !IsDocumentSurfaceDetached;
}
```

### CotizacionesPage.xaml (View del módulo piloto)

```xaml
<!-- Tres estados visuales posibles:
     1. Normal (grid visible, surface oculta): IsDocumentSurfaceVisible=false, IsDocumentSurfaceDetached=false
     2. Content Replacement (surface visible, grid oculto): IsDocumentSurfaceVisible=true, IsDocumentSurfaceDetached=false
     3. Detached (ambos visibles side-by-side): IsDocumentSurfaceVisible=true, IsDocumentSurfaceDetached=true -->
<Grid Grid.Row="2">
    <!-- ═══ Modo Normal / Content Replacement ═════════════════════════════ -->
    <Grid Visibility="{x:Bind ViewModel.IsDocumentSurfaceDetached, Mode=OneWay, 
                               Converter={StaticResource InverseBoolToVisibilityConverter}}">
        <!-- Grid de listado (visible cuando IsDocumentSurfaceVisible=false) -->
        <Border Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay, 
                                     Converter={StaticResource InverseBoolToVisibilityConverter}}">
            <ListView ItemsSource="{x:Bind ViewModel.Cotizaciones, Mode=OneWay}" ... />
        </Border>
        <!-- Document Surface (visible cuando IsDocumentSurfaceVisible=true) -->
        <ContentPresenter Content="{x:Bind ViewModel.DocumentSurfaceContent, Mode=OneWay}"
                          Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay}"/>
    </Grid>

    <!-- ═══ Modo Detached (Split View: Grid + Surface) ═══════════════════ -->
    <Grid Visibility="{x:Bind ViewModel.IsDocumentSurfaceDetached, Mode=OneWay, 
                               Converter={StaticResource BoolToVisibilityConverter}}">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="2*" MinWidth="400"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="3*" MinWidth="600"/>
        </Grid.ColumnDefinitions>
        <!-- Grid de listado (lado izquierdo) -->
        <Border Grid.Column="0"><ListView ... /></Border>
        <!-- Separador visual -->
        <Border Grid.Column="1" Width="1" Background="#E5E5E5"/>
        <!-- Document Surface (lado derecho) -->
        <ContentPresenter Grid.Column="2" Content="{x:Bind ViewModel.DocumentSurfaceContent, Mode=OneWay}"/>
    </Grid>
</Grid>
```

### CotizacionDocumentoPage.xaml (Document Surface)

```xaml
<CommandBar>
    <!-- Comandos primarios: Volver a Lista, Guardar, Agregar Línea, etc. -->
    ...
    <!-- Document Surface Detachable Mode: acción avanzada opcional -->
    <CommandBar.SecondaryCommands>
        <AppBarButton x:Name="BtnToggleDetach" Label="Desacoplar Surface" Click="BtnToggleDetach_Click">
            <AppBarButton.Icon><FontIcon Glyph="&#xE89A;"/></AppBarButton.Icon>
        </AppBarButton>
    </CommandBar.SecondaryCommands>
</CommandBar>
```

### CotizacionDocumentoPage.xaml.cs (Code-behind document)

```csharp
/// <summary>
/// Callback invocado cuando el usuario hace clic en "Desacoplar Surface".
/// Alterna entre modo acoplado (content replacement) y modo desacoplado (split view).
/// </summary>
public Action? ToggleDetach { get; set; }

private void BtnToggleDetach_Click(object sender, RoutedEventArgs e)
{
    ToggleDetach?.Invoke();
}
```

### CotizacionesPage.xaml.cs (Code-behind módulo)

```csharp
private void BtnNueva_Click(object sender, RoutedEventArgs e)
{
    var page = new CotizacionDocumentoPage(null);
    page.ViewModel.DocumentSaved = OnDocumentSaved;
    page.VolverALista = OnVolverALista;
    page.ToggleDetach = OnToggleDetach; // ADR-027: wire detachable mode callback
    ViewModel.DocumentSurfaceContent = page;
    ViewModel.IsDocumentSurfaceVisible = true;
}

private void OnToggleDetach()
{
    ViewModel.ToggleDetachCommand.Execute(null);
}
```

**Decisión de scope**:
- **SOLO piloto Cotizaciones**: validar UX, estabilidad runtime, aceptación operacional antes de expandir
- **NO expandir todavía a**: Clientes, Productos, Pedidos, Ventas, OT (esperar validación piloto)
- **SOLO 1 surface desacoplada por módulo**: límite arquitectónico obligatorio, NO permitir múltiples surfaces simultáneas
- **NO floating windows OS**: mantener embedded layout limpio, NO dock managers enterprise
- **NO rehacer WorkspaceService / Shell / Runtime Observability**: extensión UX pura en capa presentación
- **Workspace Tabs NO se reemplazan**: tabs siguen reservados para workflows largos/persistentes/multi-documento complejos

**Runtime estable esperado**: Con el Detachable Mode implementado, el ERP debe soportar:
- Modo normal: operación diaria rápida con content replacement (grid XOR surface)
- Modo desacoplado: multitarea ligera ocasional (grid + surface simultáneos)
- Nueva Cotización → desacoplar → consultar grid → editar surface → guardar → volver automático a grid
- Editar Cotización → desacoplar → comparar con otra cotización en grid → acoplar nuevamente → seguir editando
- Navegación fluida sin degradación UX perceptible
- State preservation: filtros, selección, scroll mantienen durante acoplar/desacoplar
- Sin DbContext concurrency adicional (compatible con ADR-026 single-flight guard)
- Runtime Diagnostic Panel refleja modo normal/detached correctamente

**Validación runtime requerida**:
1. Nueva Cotización → desacoplar surface → navegar grid mientras edita → acoplar nuevamente → guardar
2. Editar Cotización → desacoplar → seleccionar otra cotización en grid (sin abrir) → comparar información → acoplar → continuar editando
3. Abrir/cerrar Document Surface repetidamente → confirmar reset correcto de estado detached
4. Navegar entre módulos (Clientes ↔ Cotizaciones) con surface desacoplada activa → confirmar lifecycle correcto
5. Validar ausencia de overlap visual, layout responsive correcto, split view limpio
6. Confirmar ausencia de regresiones DbContext concurrency (integración ADR-026)

**Anti-patterns documentados**:
```csharp
// ❌ NO permitir múltiples surfaces desacopladas simultáneas
if (DetachedSurfacesCount > 1) // PROHIBIDO

// ❌ NO usar floating windows OS
new Window { Content = surface }; // PROHIBIDO

// ❌ NO implementar dock manager complejo
DockPanel.Dock(surface, DockPosition.Right); // PROHIBIDO

// ❌ NO hacer split view permanente por defecto
IsDocumentSurfaceDetached = true; // por defecto, PROHIBIDO (debe ser false)

// ❌ NO volver a Workspace Tabs para CRUDs simples
_workspace.OpenTab(...); // para nueva/editar cotización, PROHIBIDO (usar Document Surface)

// ❌ NO desacoplar sin Document Surface activo
if (!IsDocumentSurfaceVisible) ToggleDetach(); // guard obligatorio en ToggleDetach()
```

**UX principles institucionalizados**:
- **Simple por defecto**: content replacement (grid XOR surface) es el comportamiento normal
- **Poderoso opcionalmente**: detachable mode (grid + surface) disponible bajo demanda usuario
- **Acción avanzada discreta**: botón "Desacoplar Surface" en CommandBar secundario, NO primario
- **Multitarea ligera controlada**: 1 surface desacoplada máximo, NO caos multi-ventana
- **Clean ERP aesthetic**: Outlook 2026 style minimalista preservado, NO enterprise dock systems pesados
- **Operacional PYME-friendly**: el ERP debe sentirse rápido, limpio, estable, profesional
- **Workspace Tabs para workflows importantes**: tabs workspace siguen siendo para multi-documento complejo, OT complejas, análisis persistente

**Documentación actualizada**:
- `Documentation/CLAUDE_RULES.md`: nueva subsección "Document Surface Detachable Mode" con UX rules, límites, anti-patterns
- `Documentation/ARCHITECTURE_STATUS.md`: estado UX Document Surface Detachable Mode piloto Cotizaciones
- `Documentation/KNOWN_ISSUES.md`: limitaciones conocidas (piloto solo Cotizaciones, 1 surface desacoplada máximo)
- `Documentation/DECISIONS.md`: este ADR-027

---

## ADR-026 — Security Runtime Concurrency Stabilization

**Decisión**: Implementar **single-flight pattern** en `PermisoService.TienePermisoAsync` y `ObtenerPermisosEfectivosAsync` usando `SemaphoreSlim` global para serializar evaluaciones de permisos runtime y eliminar `System.InvalidOperationException: "A second operation was started on this context instance before a previous operation completed."` causado por navegación rápida, Document Surface activation, pre-checks de autorización concurrentes, y bindings runtime múltiples.

**Problema identificado**:
- **DbContext concurrency exceptions**: navegación rápida entre módulos (Clientes, Cotizaciones, Pedidos), activación/desactivación de Document Surfaces, Workspace tabs, y refresh simultáneos provocaban múltiples llamadas concurrentes a `PermisoService.TienePermisoAsync(...)` usando el mismo `ErpDbContext` scoped
- **OnNavigatedTo + bindings runtime**: eventos `OnNavigatedTo`, pre-checks de autorización en comandos, y bindings de permisos en UI disparaban evaluaciones concurrentes de autorización
- **Runtime Diagnostic Panel + observability**: refresh automático del panel diagnóstico + navegación usuario + pre-checks módulo creaban colisiones de concurrencia DbContext
- **Document Surface activation**: abrir/cerrar Document Surfaces embebidos ejecutaba pre-checks autorización mientras otros módulos aún estaban evaluando permisos
- **Navegación Workspace**: cambio rápido de tabs workspace (Cotizaciones → Pedidos → Clientes) multiplicaba las evaluaciones de permisos concurrentes

**Alternativas consideradas**:
- **Per-permission semaphore con ConcurrentDictionary**: más eficiente (permite paralelismo entre permisos diferentes) pero agrega complejidad de tracking y limpieza de diccionario creciente
- **Lock statement tradicional**: sintaxis más simple pero semánticamente equivalente a semaphore; semaphore es más explícito para async/await
- **Task.Run para aislar DbContext**: anti-pattern prohibido (ADR-020), crea race conditions y problemas de scope DI
- **Cambiar DbContext a singleton**: viola arquitectura EF Core scoped, causaría más problemas de concurrency y state management
- **Cachear agresivamente todos los permisos**: `MemoryPermissionCache` ya existe para `ObtenerPermisosEfectivosAsync` pero no elimina el problema en `TienePermisoAsync` individual
- **Rehacer Security Foundation**: NO permitido (regla crítica §3 CLAUDE_RULES.md), el problema es runtime concurrency, no el modelo de seguridad

**Razón**: SemaphoreSlim global es:
- **Simple y predecible**: un solo punto de serialización, fácil de entender y mantener, sin complejidad de tracking per-key
- **DbContext-safe garantizado**: elimina completamente la posibilidad de operaciones concurrentes en el mismo `ErpDbContext` scoped durante evaluación de permisos
- **Compatible con arquitectura existente**: NO toca Security Foundation, WorkspaceService, Shell, Runtime Observability, ni modelo de permisos
- **Performance aceptable**: autorización runtime no es hot path crítico; serialización agrega latencia mínima (microsegundos) comparada con queries EF Core (milisegundos)
- **Correctitud preservada**: la lógica de autorización (override usuario → perfiles → roles) permanece idéntica, solo se serializa la ejecución
- **Double-check optimization**: en `ObtenerPermisosEfectivosAsync`, se valida caché antes y después del lock para minimizar trabajo duplicado
- **Exception-safe**: `try/finally` garantiza que el semaphore siempre se libera, evitando deadlocks
- **Runtime observability compatible**: no interfiere con `RuntimeDiagnosticService`, `OperationalObservabilityService` ni `CurrentContextTracker` (singleton sin DbContext)

**Impacto**:

### PermisoService.cs

```csharp
/// <summary>
/// Single-flight guard para serializar evaluaciones de permisos runtime.
/// Previene concurrencia DbContext durante navegación/autorización simultánea.
/// </summary>
private static readonly SemaphoreSlim _authSemaphore = new(1, 1);

public async Task<bool> TienePermisoAsync(
    Guid usuarioId, string clave, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(clave))
        return false;

    await _authSemaphore.WaitAsync(ct);
    try
    {
        // Lógica de evaluación: override → perfiles → roles
        // (sin cambios en la lógica de negocio)
    }
    finally
    {
        _authSemaphore.Release();
    }
}

public async Task<IReadOnlySet<string>> ObtenerPermisosEfectivosAsync(
    Guid usuarioId, CancellationToken ct = default)
{
    // Primer check: caché rápido sin lock
    var cached = await _cache.GetPermisosAsync(usuarioId, ct);
    if (cached.Count > 0)
        return new HashSet<string>(cached, StringComparer.OrdinalIgnoreCase);

    await _authSemaphore.WaitAsync(ct);
    try
    {
        // Double-check después del lock: otro thread pudo cachear
        cached = await _cache.GetPermisosAsync(usuarioId, ct);
        if (cached.Count > 0)
            return new HashSet<string>(cached, StringComparer.OrdinalIgnoreCase);

        // Evaluación completa: overrides + perfiles + roles
        // Cache resultado antes de retornar
    }
    finally
    {
        _authSemaphore.Release();
    }
}
```

**Decisión de scope**:
- **NO crear framework de locking complejo**: el guard simple resuelve el problema sin overhead innecesario
- **NO cambiar arquitectura DI**: `ErpDbContext` sigue scoped, `PermisoService` sigue scoped, `MemoryPermissionCache` sigue singleton
- **NO modificar Security Foundation**: modelo de permisos (override → perfiles → roles) permanece intacto
- **NO modificar WorkspaceService / Shell / Runtime Observability**: estos componentes ya son DbContext-safe
- **NO agregar telemetría enterprise**: si ocurre deadlock, se detecta en runtime; no justifica infraestructura adicional

**Runtime estable esperado**: Con el single-flight guard aplicado, el ERP debe soportar:
- Navegación rápida multi-módulo (Clientes ↔ Cotizaciones ↔ Pedidos)
- Activación/desactivación de Document Surfaces sin exceptions
- Múltiples tabs Workspace abiertos con pre-checks concurrentes
- Refresh automático Runtime Diagnostic Panel sin colisiones
- Bindings runtime de permisos en UI estables
- Pre-checks autorización en comandos async seguros

**Validación runtime requerida**:
1. Navegar rápidamente entre Clientes, Cotizaciones, Pedidos (cambiar tabs cada 1-2 segundos)
2. Abrir/cerrar Document Surfaces (Nueva Cotización → guardar → editar → volver) repetidamente
3. Ejecutar múltiples refresh simultáneos (F5 en varios módulos)
4. Validar ausencia de `System.InvalidOperationException` relacionada con DbContext
5. Confirmar autorización consistente (permisos correctos aplicados)
6. Verificar navegación fluida sin degradación UX perceptible

**Anti-patterns documentados**:
```csharp
// ❌ NO usar Task.Run para aislar DbContext
Task.Run(() => await _permisos.TienePermisoAsync(...));

// ❌ NO cambiar DbContext a singleton
services.AddSingleton<ErpDbContext>();  // PROHIBIDO

// ❌ NO capturar DbContext en campos static
private static ErpDbContext _ctx;  // PROHIBIDO

// ❌ NO usar lock tradicional sin considerar async
lock (_lock) { await _context.SaveChangesAsync(); }  // Deadlock risk
```

**Documentación actualizada**:
- `Documentation/CLAUDE_RULES.md` §8: nueva subsección "Security Runtime Concurrency" con patrón single-flight autorización
- `Documentation/KNOWN_ISSUES.md`: nuevo issue documentando problema original, causa raíz, solución aplicada
- `Documentation/ARCHITECTURE_STATUS.md`: estado runtime security stabilization
- `Documentation/DECISIONS.md`: este ADR-026

---

## ADR-025 — Document Surface UX Pattern: Contextual Embedded Editing for CRUD Operations

**Decisión**: Implementar **Document Surface UX Pattern** para operaciones CRUD ligeras (Nuevo/Editar/Abrir) en módulos seleccionados (piloto: Cotizaciones, Clientes, Productos), usando un **ContentPresenter embebido** que reemplaza temporalmente el grid de listado dentro del módulo activo, en lugar de abrir Workspace Tabs persistentes.

**Problema identificado**:
- **Caos de Workspace Tabs innecesarios**: operaciones CRUD simples como "Nueva Cotización" o "Editar Cliente" generaban tabs persistentes en el Workspace Layer, causando acumulación excesiva de tabs para tareas que normalmente se completan en una sola sesión
- **Pérdida de contexto de módulo**: al abrir un tab workspace para editar, el usuario perdía visibilidad del listado/grid del módulo activo
- **UX fragmentada**: navegación entre módulo (para buscar) y workspace (para editar) creaba fricción operacional innecesaria
- **Flujo PYME ineficiente**: el flujo típico `crear → guardar → seguir trabajando en lista` requería cerrar tab manualmente o tener múltiples tabs abiertos sin necesidad
- **Workspace Tabs infrautilizados**: tabs persistentes son valiosos para workflows complejos/multi-documento, pero estaban siendo usados también para operaciones contextuales ligeras

**Alternativas consideradas**:
- **Mantener Workspace Tabs para todo**: simple pero perpetúa el caos de tabs, no mejora UX operacional
- **Modal dialogs para edición**: limitados en espacio, difíciles para formularios complejos con múltiples secciones
- **Split view permanente (listado | surface)**: ocupa mucho espacio, ruido visual constante, complica layouts responsive
- **Master-detail layout anidado**: overhead arquitectónico, complejidad de navegación, no cumple estilo Outlook 2026 minimalista
- **Docking manager / floating windows**: fuera de scope para ERP PYME, overhead innecesario
- **Rediseñar WorkspaceService / Shell**: NO permitido (regla crítica §3 CLAUDE_RULES.md), arquitectura estable

**Razón**: Document Surface embebido con content replacement es:
- **No invasivo arquitectónicamente**: preserva WorkspaceService, Shell, Runtime Observability completamente intactos; solo agrega nueva capacidad UX en capa presentación
- **UX limpia PYME-friendly**: un solo contenido visible a la vez (grid XOR surface), enfoque claro, menos ruido visual
- **Contexto preservado**: usuario permanece en el módulo activo, sabe dónde está ("Cotizaciones"), ve título y puede volver al listado fácilmente
- **Flujo natural crear/editar/volver**: botón "← Volver a Lista" explícito, guardar cierra automáticamente y refresca grid
- **Workspace Tabs para workflows importantes**: libera tabs para documentos persistentes/complejos (OT multi-paso, comparación multi-documento, workflows largos)
- **Transición instantánea/sutil**: sin animaciones complejas, performance óptima, ERP operacional rápido
- **Piloto controlado**: validar UX en 3 módulos (Cotizaciones ✅, Clientes, Productos) antes de expandir
- **Escalable**: patrón aplicable a cualquier módulo CRUD ligero futuro sin cambios arquitectónicos

**Impacto**:

### CotizacionesViewModel (ViewModel del módulo listado)

```csharp
// Document Surface UX Pattern state
[ObservableProperty] private bool isDocumentSurfaceVisible;
[ObservableProperty] private object? documentSurfaceContent;

public void AbrirNuevaCotizacion()
{
    DocumentSurfaceContent = null;
    IsDocumentSurfaceVisible = true;
}

public void AbrirEditarCotizacion(CotizacionDto cotizacion)
{
    DocumentSurfaceContent = cotizacion;
    IsDocumentSurfaceVisible = true;
}

public async Task CerrarDocumentSurfaceAsync()
{
    IsDocumentSurfaceVisible = false;
    DocumentSurfaceContent = null;
    await RefrescarAsync(); // Refrescar grid automáticamente
}
```

### CotizacionesPage.xaml (View del módulo)

```xaml
<Grid Grid.Row="2">
    <!-- Listado (visible cuando IsDocumentSurfaceVisible = false) -->
    <Border Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay, 
                                  Converter={StaticResource InverseBoolToVisibilityConverter}}">
        <Grid>
            <ListView ItemsSource="{x:Bind ViewModel.Cotizaciones, Mode=OneWay}" ... />
            <ProgressRing IsActive="{x:Bind ViewModel.IsBusy, Mode=OneWay}" ... />
            <ContentControl ContentTemplate="{StaticResource ErpGridEmptyTemplate}" ... />
        </Grid>
    </Border>

    <!-- Document Surface (visible cuando IsDocumentSurfaceVisible = true) -->
    <ContentPresenter x:Name="DocumentSurfacePresenter"
                      Content="{x:Bind ViewModel.DocumentSurfaceContent, Mode=OneWay}"
                      Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay}"/>
</Grid>
```

### CotizacionesPage.xaml.cs (Code-behind del módulo)

```csharp
// ANTES (Workspace Tab):
private void BtnNueva_Click(object sender, RoutedEventArgs e)
{
    var tempKey = $"cotizacion-nueva-{Guid.NewGuid():N}";
    _workspace.OpenTab(
        key: tempKey, title: "Nueva Cotizacion", icon: "",
        pageFactory: () => new CotizacionDocumentoPage(null),
        isClosable: true);
}

// DESPUÉS (Document Surface):
private void BtnNueva_Click(object sender, RoutedEventArgs e)
{
    var page = new CotizacionDocumentoPage(null);
    page.ViewModel.DocumentSaved = OnDocumentSaved;
    page.VolverALista = OnVolverALista;
    ViewModel.DocumentSurfaceContent = page;
    ViewModel.IsDocumentSurfaceVisible = true;
}

private async void OnDocumentSaved()
{
    await ViewModel.CerrarDocumentSurfaceAsync();
    ViewModel.SuccessMessage = "Cotización guardada correctamente.";
}

private async void OnVolverALista()
{
    await ViewModel.CerrarDocumentSurfaceAsync();
}
```

### CotizacionDocumentoViewModel (ViewModel del documento)

```csharp
// Document Surface UX Pattern callback
public Action? DocumentSaved;

[RelayCommand]
public async Task GuardarAsync(CancellationToken ct = default)
{
    // ... validaciones y lógica de guardado ...

    if (IsNuevo)
    {
        var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
        if (!r.Success) { ErrorMessage = r.Error; return; }
        SuccessMessage = $"Cotización #{r.Value!.Id} creada.";
        Initialize(r.Value);
        DocumentSaved?.Invoke(); // ← Notificar al módulo padre
    }
    else
    {
        var r = await _service.ActualizarAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
        if (!r.Success) { ErrorMessage = r.Error; return; }
        SuccessMessage = "Cotización actualizada.";
        _documento = r.Value;
        DocumentSaved?.Invoke(); // ← Notificar al módulo padre
    }
}
```

### CotizacionDocumentoPage.xaml (View del documento)

```xaml
<CommandBar>
    <!-- Document Surface UX Pattern: botón para volver al listado -->
    <AppBarButton x:Name="BtnVolverALista" Label="Volver a Lista" Click="BtnVolverALista_Click">
        <AppBarButton.Icon><FontIcon Glyph="&#xE72B;"/></AppBarButton.Icon>
    </AppBarButton>
    <AppBarSeparator/>
    <AppBarButton Label="Guardar" Command="{x:Bind ViewModel.GuardarCommand}">
        <AppBarButton.Icon><FontIcon Glyph="&#xE74E;"/></AppBarButton.Icon>
    </AppBarButton>
    <!-- ... otros botones de workflow (Enviar, Aprobar, Convertir, Cancelar) ... -->
</CommandBar>
```

### CotizacionDocumentoPage.xaml.cs (Code-behind del documento)

```csharp
public Action? VolverALista { get; set; }

private void BtnVolverALista_Click(object sender, RoutedEventArgs e)
{
    VolverALista?.Invoke();
}
```

### InverseBoolToVisibilityConverter (nuevo converter)

```csharp
// Ybridio.WinUI/Converters/InverseBoolToVisibilityConverter.cs
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
            return b ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
            return v == Visibility.Collapsed;
        return false;
    }
}
```

**Reglas UX oficiales**:

1. **Layout: Content Replacement**
   - ✅ ContentPresenter o panel reemplazable dentro del módulo
   - ✅ Un solo contenido visible: grid XOR Document Surface
   - ❌ NO split view permanente
   - ❌ NO grid de dos columnas (listado | surface)
   - **Razón**: UX más limpia, menos ruido visual, mayor enfoque operacional

2. **Transiciones**
   - ✅ Transición instantánea o muy sutil
   - ✅ Cambio directo de visibilidad mediante binding
   - ❌ NO animaciones complejas
   - **Razón**: ERP operacional debe sentirse rápido, fluidez > efectos visuales

3. **Comportamiento Guardar**
   - ✅ Después de guardar: refrescar grid, cerrar surface, volver al listado
   - ✅ Flujo PYME típico: `crear → guardar → seguir trabajando en lista`
   - ❌ NO dejar surface abierto automáticamente para CRUDs ligeros
   - **Excepción futura**: workflows largos/OT complejas

4. **Navegación "← Volver a Lista"**
   - ✅ Primer botón en CommandBar del Document Surface
   - ✅ Texto claro: "Volver a Lista" o "← Volver"
   - ✅ Icon: `&#xE72B;` (Back)
   - ✅ Acción: cerrar surface sin guardar, volver al grid
   - **Razón**: permitir cancelar, navegación explícita, contexto visual claro

5. **Migración Inicial (Piloto)**
   - ✅ Aplicar PRIMERO: Cotizaciones ✅, Clientes (pendiente), Productos (pendiente)
   - ❌ NO migrar todavía: Pedidos (workflow complejo), Ventas (genera otros documentos), OT (multi-paso)
   - **Razón**: validar UX, observar aceptación operacional, estabilidad runtime

6. **Workflows Complejos**
   - ✅ Workflows complejos permanecen usando Workspace Tabs persistentes
   - ✅ Document Surface para: CRUD rápido, edición ligera, mantenimiento contextual
   - ❌ NO usar Document Surface para: OT complejas, multi-documento, comparación/análisis, workflows largos
   - **Razón**: preservar Workspace Tabs para su propósito original (persistencia, multi-documento, complejidad)

**Principio arquitectónico oficial**:

```
Workspace Tabs      = workflows persistentes, multi-documento, complejos, importantes
Document Surfaces   = operación rápida contextual (Nuevo/Editar/Abrir) sin tab persistente
```

**Anti-patterns formalizados**:

- ❌ Usar Document Surface para workflows complejos/multi-documento
- ❌ Dejar surface abierto después de guardar (para CRUDs ligeros)
- ❌ Implementar animaciones complejas de transición
- ❌ Usar split view o layouts master-detail permanentes
- ❌ Abrir Workspace Tabs para operaciones CRUD simples (Nueva Cotización, Editar Cliente)
- ❌ Migrar todos los módulos de golpe sin validar piloto
- ❌ Modificar/rehacer WorkspaceService, Shell, Runtime Observability

**Decisión de scope**:

- **NO**: rehacer WorkspaceService, navegación, Shell, Runtime Observability
- **SOLO**: agregar Document Surface UX Pattern en capa presentación (Views/ViewModels)
- **NO**: afectar workflows complejos que requieren Workspace Tabs
- **NO**: eliminar Workspace Tabs (preservar para workflows persistentes)

**Documentación actualizada**:
- `Documentation/CLAUDE_RULES.md`: nueva sección §12 "Document Surface UX Pattern (§ADR-025)" con reglas oficiales UX, arquitectura del pattern, anti-patterns
- `Documentation/ARCHITECTURE_STATUS.md`: nueva subsección "Document Surface UX Standardization (implementado 2026-05-09)"
- `Documentation/DECISIONS.md`: este ADR-025

**Runtime estable**: Build exitoso (1 succeeded, 0 failed), navegación fluida preservada, Document Surface funcional en Cotizaciones (Nueva/Editar/Volver a Lista), WorkspaceService intacto, Runtime Observability preservado, sin regresiones.

**Validación UX requerida**:
1. ✅ Menos caos de Workspace Tabs (operaciones CRUD no generan tabs innecesarios)
2. ✅ Navegación más natural (usuario permanece en contexto de módulo)
3. ✅ Contexto de módulo preservado (grid oculto temporalmente, no perdido)
4. ✅ Operación más rápida (sin navegación Workspace ↔ Módulo)
5. ✅ Flujo PYME cumplido (crear → guardar → cerrar automático → seguir trabajando)
6. ✅ Runtime Observability funcional (reportes de contexto correctos)
7. ✅ WorkspaceService intacto (workflows complejos siguen usando tabs persistentes)

**Próximos pasos piloto**:
- Replicar pattern a Clientes (ClientesPage → ClienteDocumentoPage)
- Replicar pattern a Productos (ProductosPage → ProductoDocumentoPage)
- Validar aceptación operacional con usuarios finales
- Confirmar estabilidad runtime y observabilidad correcta
- Expandir gradualmente a otros módulos CRUD ligeros según validación

---

## ADR-024 — Workspace TabView Content Host Separation: Header/Content Layout Fix

**Decisión**: Implementar **Padding top estructural de 60px** directamente en el `WorkspaceTabView` (ShellPage.xaml) para crear separación física real entre el header region (TabViewItem + SelectionIndicator) y el content host, eliminando el overlap visual final donde el underline azul de selección invadía el contenido documental.

**Problema identificado**: A pesar de ADR-022 (estilos diferenciados `WorkspaceTabItemStyle` vs `OutlookTabItemStyle` + margin 12px) y ADR-023 (Border wrapper con background sólido para Module Layer), persistía un **overlap visual interno en el WorkspaceTabView** donde:
- **Header region invade content host**: WinUI TabView coloca el TabViewItem header (48px MinHeight) y SelectionBar (4px) **inmediatamente adyacentes** al content host sin separación vertical estructural
- **Underline azul cae encima del contenido**: el SelectionBar de 4px de altura visualmente invade la región superior del contenido documental
- **Contenido inicia demasiado arriba**: los documentos abiertos (Cotización, Pedido, Venta, OT) aparecen pegados al header, sin espacio respiratorio
- **Sensación de ensimamiento visual**: tabs workspace parecen "apilados sobre" el contenido en lugar de separados jerárquicamente
- **Margin insuficiente**: el `Margin="0,12,0,0"` solo separa el WorkspaceTabView del ModuleFrame, NO separa el content host interno del header region

**Alternativas consideradas**:
- **Solo aumentar Margin top en WorkspaceTabView** (de 12px a 60px+): NO resuelve el problema porque el margin separa el TabView del ModuleFrame, NO el content host interno del header
- **Margins arbitrarios gigantes en contenido** (100px, 150px top margin en cada documento): hack frágil, NO estructural, rompe responsive, difícil mantener
- **TranslateTransform / RenderTransform offsets**: hack visual, NO solución estructural, riesgo layout roto en DPI alto o resize
- **Z-index tricks / Opacity manipulations**: NO resuelve overlap real, solo lo oculta visualmente
- **Rediseñar TabView template completo**: overhead innecesario, riesgo romper WinUI internals, difícil mantenibilidad
- **Usar Border wrapper dentro del TabView content**: agrega container extra innecesario, complejidad sin beneficio

**Razón**: Padding top estructural directo en el TabView es:
- **No invasivo**: solo modifica `ShellPage.xaml` línea 308, NO toca WorkspaceService, navegación, Runtime Observability, estilos globales
- **Estructural y limpio**: separación física declarativa mediante Padding XAML, NO hacks visuales
- **Calculado precisamente**: 48px (TabItem MinHeight) + 4px (SelectionBar) + 8px (espaciado visual) = **60px total**
- **Performante**: sin overhead runtime, solo declarativo XAML estático
- **Responsive**: Padding se escala correctamente con DPI y window resize
- **Mantenible**: solución simple, documentada, fácil ajustar si cambian dimensiones de TabItem/SelectionBar
- **Consistente con ADR-023**: análogo al Border wrapper con Padding usado para Module Layer, pero aplicado al content host interno en lugar de container externo

**Impacto**:

**ShellPage.xaml (líneas 302-315)**:
```xaml
<!-- Capa 2: workspace persistente (se superpone cuando hay tabs) -->
<!--
    Padding top estructural (60px) separa físicamente el content host del header region:
    - TabItem MinHeight: 48px
    - SelectionBar: 4px
    - Espaciado visual: 8px
    Total: 60px evita overlap real entre header/selection y contenido documental
-->
<TabView x:Name="WorkspaceTabView"
         IsAddTabButtonVisible="False"
         TabWidthMode="SizeToContent"
         Visibility="Collapsed"
         Margin="0,12,0,0"
         Padding="0,60,0,0"
         Background="{ThemeResource LayerOnMicaBaseAltFillColorDefaultBrush}"
         TabCloseRequested="WorkspaceTabView_TabCloseRequested"
         SelectionChanged="WorkspaceTabView_SelectionChanged">
    <TabView.Resources>
        <Style TargetType="TabViewItem" BasedOn="{StaticResource WorkspaceTabItemStyle}"/>
    </TabView.Resources>
</TabView>
```

**Diferencia con Module Layer (ADR-023)**:
- **Module TabViews**: usan **Border wrapper externo** con `Padding="16,12,16,16"` para crear **container boundary** físico (separación del Workspace Layer superior)
- **WorkspaceTabView**: usa **Padding interno directo** `Padding="0,60,0,0"` para separar **header region del content host** (separación interna del propio TabView)
- Ambos son soluciones estructurales complementarias: uno separa containers entre layers, otro separa header/content dentro del workspace host

**Regla formalizada**:

El **WorkspaceTabView** en ShellPage.xaml DEBE tener:
1. **Margin top (12px)**: separa el TabView del ModuleFrame (layer external separation)
2. **Padding top (60px)**: separa el content host del header region (internal host separation)

**Cálculo de Padding obligatorio**:
```
Padding top = TabItem.MinHeight + SelectionBar.Height + Visual Spacing
            = 48px + 4px + 8px
            = 60px
```

Si cambian `WorkspaceTabItemStyle` dimensiones (MinHeight, SelectionBar height), recalcular Padding proporcionalmente.

**Anti-patterns formalizados**:
- ❌ WorkspaceTabView sin Padding top (overlap header/content host inevitable)
- ❌ Usar solo Margin para separación interna content host (Margin NO afecta content host interno)
- ❌ Margins arbitrarios en documentos individuales (hack frágil, NO escalable)
- ❌ TranslateTransform/RenderTransform offsets (hack visual, NO estructural)
- ❌ Z-index tricks (NO resuelve overlap real)
- ❌ Rediseñar TabView template completo (overhead innecesario)

**Resultado UX esperado**:

El usuario debe percibir claramente:
✅ **Workspace Header separado físicamente del contenido documental** — NO underline invadiendo contenido  
✅ **Documento activo con espacio respiratorio superior** — contenido NO pegado al header  
✅ **Module Tabs completamente debajo del Workspace Layer** — jerarquía clara sin ensimamiento  
✅ **Sin tabs ensimados ni overlap visual** — separación estructural real header/content  
✅ **UX limpia y profesional** — ERP operacional estable, Outlook 2026 style  

El ERP debe sentirse:
- **Estable** — separación estructural predecible, NO overlap dinámico
- **Profesional** — workspace dominante, contenido organizado, jerarquía clara
- **Limpio** — sin invasiones visuales, espaciado correcto
- **Operacional** — trabajo multi-documento durante horas sin confusión visual
- **Moderno** — Outlook 2026 minimalista, subtle, elegant

**Decisión de scope**:
- **NO**: rehacer WorkspaceService, navegación, Shell, estilos globales, TabView template
- **SOLO**: agregar Padding top estructural en WorkspaceTabView para separar header/content host
- **NO**: introducir hacks visuales, margins arbitrarios, transforms, z-index tricks
- **NO**: afectar Runtime Observability, performance navegacional, responsive behavior

**Documentación actualizada**:
- `Documentation/CLAUDE_RULES.md`: nueva sección "TabView Content Host Separation (Workspace Layer)" con cálculo de Padding, reglas obligatorias, anti-patterns, diferencia Module/Workspace
- `Documentation/ARCHITECTURE_STATUS.md`: nueva subsección "Workspace TabView Content Host Separation (implementado 2026-05-09)"
- `Documentation/DECISIONS.md`: este ADR-024

**Runtime estable**: Build exitoso (1 succeeded, 0 failed), navegación fluida preservada, eliminación completa del overlap visual header/content host, UX workspace limpia y profesional sin regresiones.

**Validación requerida**: Abrir múltiples documentos (Nueva Cotización, Pedido, Venta, OT) y verificar visualmente: 1) underline azul NO invade contenido, 2) tabs NO parecen ensimados, 3) separación clara entre Workspace Header y contenido documental, 4) UX limpia sin overlap, 5) responsive correcto en múltiples DPI/resoluciones.

---

## ADR-023 — Workspace Visual Container Hierarchy: Module Layer Physical Separation

**Decisión**: Implementar **visual container hierarchy** mediante Border wrapper obligatorio para todos los Module TabViews, con `Padding="16,12,16,16"` y `Background="{ThemeResource LayerFillColorDefaultBrush}"`, y cambiar Module TabView `Background` de `ApplicationPageBackgroundThemeBrush` a `"Transparent"` (el fondo lo provee el Border).

**Problema identificado**: A pesar de ADR-022 (estilos diferenciados `WorkspaceTabItemStyle` vs `OutlookTabItemStyle` + margin 12px en WorkspaceTabView), persistía el efecto visual **"tabs transparentes/ensimados"** porque:
- **Falta de container boundary físico**: Module TabViews estaban directamente en la Page raíz sin separación visual real del Workspace Layer
- **Background similar**: tanto Workspace como Module usaban backgrounds sutiles similares (`ApplicationPageBackgroundThemeBrush`), generando sensación de continuidad visual
- **Sin padding superior explícito**: tabs parecían "pegados" al borde de la página
- **Margin insuficiente**: el margin 12px en Workspace no creaba boundary visible en el Module Layer
- **Efecto "tabs flotando sobre tabs"**: usuario no podía diferenciar claramente el Module Layer como contenido del documento activo vs Workspace Layer como documentos persistentes externos

**Alternativas consideradas**:
- **Solo aumentar margin en WorkspaceTabView** (de 12px a 24px): insuficiente, no crea boundary físico en Module Layer
- **Colores agresivos en Module TabView** (backgrounds saturados, borders llamativos): rompe estilo Outlook 2026, no profesional
- **Separator/Divider visual entre Workspace y Module**: agrega elemento UI extra, no resuelve falta de container boundary
- **Rediseñar Shell con layouts anidados**: fuera de scope, innecesario para resolver problema visual
- **Docking manager enterprise**: overhead arquitectónico, riesgo romper WorkspaceService

**Razón**: Border wrapper con fondo sólido sutil y padding superior es:
- **No invasivo**: solo toca XAML de páginas módulo (VentasPage, FinanzasPage, InventarioPage, ConfiguracionPage), NO altera WorkspaceService, navegación, Shell
- **Performante**: sin overhead runtime, solo declarativo XAML
- **Outlook 2026 compliant**: `LayerFillColorDefaultBrush` es background sutil oficial, no agresivo
- **Crea boundary físico visible**: el fondo del Border separa claramente el Module Layer del Workspace Layer
- **Padding superior real**: 12px desde borde de página crea separación física perceptible
- **Escalable**: patrón aplicable a cualquier futuro Module TabView sin refactoring
- **Elimina ensimamiento**: Module Layer ahora se siente "contenido dentro del documento activo", Workspace Layer se siente "externo/superior"

**Impacto**:

**VentasPage.xaml**:
```xaml
<Border Padding="16,12,16,16"
        Background="{ThemeResource LayerFillColorDefaultBrush}">
    <TabView x:Name="VentasTabs"
             Background="Transparent"
             TabWidthMode="SizeToContent"
             IsAddTabButtonVisible="False">
        <!-- 5 tabs: Clientes, Cotizaciones, Pedidos, Ventas, Órdenes de Trabajo -->
    </TabView>
</Border>
```

**FinanzasPage.xaml**:
```xaml
<Border Padding="16,12,16,16"
        Background="{ThemeResource LayerFillColorDefaultBrush}">
    <TabView x:Name="FinanzasTabs"
             Background="Transparent" ...>
        <!-- 4 tabs: Gastos, Ingresos, CxC, CxP -->
    </TabView>
</Border>
```

**InventarioPage.xaml**:
```xaml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>   <!-- Dashboard strip -->
        <RowDefinition Height="*"/>      <!-- TabView container -->
    </Grid.RowDefinitions>

    <!-- Row 0: Dashboard light strip preservado sin cambios -->
    <Border Grid.Row="0" Background="#FAFAFA" ...>...</Border>

    <!-- Row 1: Border wrapper para TabView -->
    <Border Grid.Row="1"
            Padding="16,12,16,16"
            Background="{ThemeResource LayerFillColorDefaultBrush}">
        <TabView x:Name="InventarioTabs"
                 Background="Transparent" ...>
            <!-- 7 tabs: Existencias, Entradas, Salidas, Kardex, Conteo, Órdenes de Compra, Productos -->
        </TabView>
    </Border>
</Grid>
```

**ConfiguracionPage.xaml**:
```xaml
<Grid>
    <!-- TabsGlobal: Border wrapper -->
    <Border Padding="16,12,16,16"
            Background="{ThemeResource LayerFillColorDefaultBrush}">
        <TabView x:Name="TabsGlobal"
                 Background="Transparent" ...>
            <!-- Tabs: Empresa, Tiendas, Auditoría, Seguridad (con nested TabsSeguridad) -->
        </TabView>
    </Border>

    <!-- TabsTienda: Border wrapper con Visibility="Collapsed" -->
    <Border Padding="16,12,16,16"
            Background="{ThemeResource LayerFillColorDefaultBrush}"
            Visibility="Collapsed">
        <TabView x:Name="TabsTienda"
                 Background="Transparent" ...>
            <!-- Tabs: Usuarios, Cajas, Dispositivos, Promociones, Almacenes, Permisos, Facturación, Personalización -->
        </TabView>
    </Border>
</Grid>
```

**Patrón container estándar formalizado**:
```xaml
<!-- Module page (obligatorio para toda página con TabView de navegación interna) -->
<Border Padding="16,12,16,16"
        Background="{ThemeResource LayerFillColorDefaultBrush}">
    <TabView x:Name="ModuleTabs"
             Background="Transparent"
             TabWidthMode="SizeToContent"
             IsAddTabButtonVisible="False"
             SelectionChanged="...">
        <!-- TabViewItems con navegación interna Frame-based -->
    </TabView>
</Border>
```

**Anti-patterns formalizados**:
- ❌ Module TabView sin container boundary físico (tabs transparentes/ensimados)
- ❌ Module TabView con `Background="Transparent"` o `Background="ApplicationPageBackgroundThemeBrush"` directo en Page (sin Border wrapper)
- ❌ Module TabView sin padding superior (pegado al borde de página, sin separación visual del Workspace)
- ❌ Depender solo de Margin para separación visual (insuficiente, necesita background boundary)
- ❌ TabView dentro de TabView sin diferenciación clara (nested TabsSeguridad ahora con Background="Transparent")

**Resultado UX esperado**:

El usuario debe percibir claramente:
✅ **Workspace Tabs = documentos persistentes externos/superiores** (dominantes, background sutil, 48px height, margin 12px)  
✅ **Module Tabs = navegación contextual contenida** (secundarios, dentro de Border con fondo sólido, 40px height)  
✅ **Separación física real entre capas** — NO tabs flotando sobre tabs  
✅ **Documento activo como superficie principal** — Module Layer se siente "contenido dentro del documento", NO continuación del Workspace  
✅ **UX limpia y operacional** — sin transparencias confusas ni tabs ensimados  

El ERP debe sentirse:
- **Estable** — jerarquía visual clara y predecible
- **Profesional** — ERP-like, no browser tabs caótico
- **Claro** — boundary físico visible, diferenciación inmediata
- **Operacional** — trabajo multi-documento sin confusión visual durante horas
- **Moderno** — Outlook 2026 style, subtle, elegant

**Decisión de scope**:
- **NO**: rehacer WorkspaceService, navegación, Shell, sistema documental
- **SOLO**: corregir container hierarchy visual mediante Border wrapper en páginas módulo
- **NO**: introducir docking manager, ribbons, rediseños arquitectónicos
- **NO**: afectar Runtime Observability ni performance navegacional
- **NO**: cambiar estilos de tabs (ADR-022 permanece vigente)

**Documentación actualizada**:
- `docs/CLAUDE_RULES.md`: sección 16 expandida con "Visual Container Hierarchy", reglas obligatorias, patrón XAML, anti-patterns
- `docs/ARCHITECTURE_STATUS.md`: nueva subsección "Visual Container Hierarchy (implementado 2026-05-09)" con problema, solución, implementación, resultado
- `docs/DECISIONS.md`: este ADR-023

**Runtime estable**: Build exitoso (1 succeeded, 0 failed), navegación fluida preservada, eliminación completa efecto "tabs ensimados", experiencia operacional mejorada sin regresiones.

---

## ADR-022 — Workspace Visual Hierarchy: dos capas visuales diferenciadas

**Decisión**: Establecer jerarquía visual clara entre **Workspace Tabs** (documentos persistentes) y **Module Tabs** (navegación interna) mediante dos estilos XAML diferenciados (`WorkspaceTabItemStyle` y `OutlookTabItemStyle`) y separación vertical explícita (margin 12px superior en WorkspaceTabView).

**Problema identificado**: Workspace Tabs (documentos abiertos: Venta #91, Pedido #55, OT #12) y Module Tabs (navegación módulo: Cotizaciones, Pedidos, Ventas) usaban el mismo estilo visual (`OutlookTabItemStyle`) y ocupaban la misma zona vertical sin separación, generando:
- **Ensimamiento visual**: ambos niveles de tabs parecían un solo control
- **Confusión operacional**: usuario no diferenciaba documentos abiertos vs navegación módulo
- **Jerarquía ambigua**: tabs persistentes no destacaban visualmente
- **UX poco clara**: navegación multi-documento generaba caos visual

**Alternativas consideradas**:
- **Docking manager enterprise** (WeifenLuo.WinFormsUI, AvalonDock): overhead arquitectónico innecesario, riesgo de romper WorkspaceService
- **Colores agresivos** (backgrounds saturados, borders llamativos): rompe estilo Outlook 2026, no profesional
- **Eliminar tabs internos de módulos**: destruye navegación operacional, regresión UX
- **Rediseñar Shell/WorkspaceService**: fuera de scope, innecesario para resolver problema visual
- **Ribbons/CommandBars**: cambia paradigma navegacional completo, no resuelve jerarquía tabs

**Razón**: La solución conservadora mediante estilos XAML diferenciados y spacing vertical es:
- **No invasiva**: solo toca `App.xaml` y `ShellPage.xaml`, no altera WorkspaceService ni navegación
- **Performante**: sin overhead runtime, solo declarativo XAML
- **Outlook 2026 compliant**: backgrounds sutiles, tipografía consistente, no agresiva
- **Clara visualmente**: workspace 48px height vs module 40px; background vs transparent; margin superior 12px
- **Escalable**: patrón aplicable a futuros TabViews sin refactoring
- **Inmediatamente perceptible**: usuario diferencia capas sin aprendizaje

**Impacto**:

**App.xaml**:
- `OutlookTabItemStyle` (Module Layer): MinHeight=40, Padding=16,8,4,8, Background=Transparent, SelectionBar=3px, CloseButton=20x20
- `WorkspaceTabItemStyle` (Workspace Layer): MinHeight=48, Padding=18,12,6,12, Background=SubtleFillColorSecondaryBrush (normal) / LayerFillColorDefaultBrush (selected), SelectionBar=4px, CloseButton=22x22

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
- Ya usan `OutlookTabItemStyle` (no requieren cambios)

**Diferenciación visual**:

| Característica | Workspace Layer | Module Layer |
|---|---|---|
| **Rol** | Documentos persistentes | Navegación módulo |
| **MinHeight** | 48px | 40px |
| **Padding** | 18,12,6,12 | 16,8,4,8 |
| **Background (normal)** | SubtleFillColorSecondaryBrush | Transparent |
| **Background (selected)** | LayerFillColorDefaultBrush | Transparent |
| **SelectionBar** | Height=4, Margin=8,0 | Height=3, Margin=6,0 |
| **CloseButton** | 22x22, "Cerrar documento" | 20x20, "Cerrar" |
| **Separación vertical** | Margin 0,12,0,0 | — |
| **Jerarquía visual** | Dominante, principal | Secundario, contextual |

**Anti-patterns evitados**:
- ❌ Usar el mismo estilo para ambas capas (confusión visual)
- ❌ Tabs workspace sin separación vertical del módulo (ensimamiento)
- ❌ Module tabs con height/padding igual a workspace (jerarquía rota)
- ❌ Backgrounds agresivos o colores llamativos (mantener Outlook 2026 sutil)
- ❌ TabView dentro de TabView sin diferenciación clara
- ❌ Workspace tabs visualmente secundarios (pierde jerarquía)
- ❌ Module tabs visualmente dominantes (compite con workspace)

**Resultado UX esperado**:

El usuario debe diferenciar inmediatamente:
✅ Documentos abiertos (Workspace tabs: más altos, background sutil, dominantes)  
✅ Navegación de módulo (Module tabs: compactos, transparentes, secundarios)  
✅ Sin ensimamiento visual ni caos de tabs  
✅ Experiencia limpia, estable, profesional, ERP-like, operacional  

El Workspace debe sentirse:
- **Limpio** — no ensimado visualmente
- **Estable** — jerarquía clara y predecible
- **Profesional** — ERP-like, no browser tabs caótico
- **Operacional** — flujo de trabajo moderno y cómodo durante horas
- **Moderno** — Outlook 2026 style, subtle, elegant

**Decisión de scope**:
- **NO**: rehacer WorkspaceService, navegación, Shell, sistema documental
- **SOLO**: corregir jerarquía visual mediante estilos y spacing
- **NO**: introducir docking manager, ribbons, o rediseños arquitectónicos
- **NO**: afectar Runtime Observability ni performance navegacional

**Documentación actualizada**:
- `docs/CLAUDE_RULES.md`: nueva sección 16 "Workspace Visual Hierarchy" con capas visuales, estilos, spacing, hierarchy intent, anti-patterns
- `docs/ARCHITECTURE_STATUS.md`: nueva sección "Workspace Visual Hierarchy" con tabla comparativa, implementación, resultado UX
- `docs/DECISIONS.md`: este ADR-022

**Runtime estable**: Build exitoso, navegación fluida, jerarquía visual clara, experiencia operacional mejorada sin regresiones.

---

## ADR-021 — Workspace Single-Instance Policy & Centralized Helper
## ADR-020 — DbContext Runtime Concurrency Rules: single-flight guard pattern

**Decisión**: Aplicar un patrón simple de **single-flight guard** (`_isRefreshing` / `_isLoading`) en TODOS los ViewModels que ejecuten comandos async con carga/refresh (`LoadAsync`, `RefrescarAsync`) que indirectamente usen `DbContext` vía Application services scoped. El guard bloquea reentrada: si un refresh está en curso, llamadas adicionales se ignoran (early return) hasta que el primero complete. El guard SIEMPRE se libera en el bloque `finally` del comando.

**Problema identificado**: `DbContext` scoped NO es thread-safe; EF Core lanza `System.InvalidOperationException: "A second operation was started on this context instance before a previous operation completed."` si dos operaciones async concurrentes intentan usar el mismo contexto. En runtime ERP esto ocurre cuando:
- Usuario navega rápidamente entre módulos (`OnNavigatedTo` → `RefrescarAsync` mientras hay refresh previo en curso)
- Usuario hace clic repetido en botones de comando async
- Multi-tab navigation rápida
- Timers de diagnóstico (aunque `DiagnosticPanelViewModel` ya es DbContext-safe porque solo lee snapshots en memoria, NO usa DbContext directamente)

**Alternativas consideradas**:
- **Locks (`SemaphoreSlim`, `lock`)**: overhead innecesario (ViewModels en UI thread, no hay paralelismo real, solo concurrencia async); complejidad de deadlocks
- **Hacer DbContext transient en lugar de scoped**: rompe filtros globales y rompe patrón establecido DI; no es solución arquitectónica correcta
- **`AsyncRelayCommand` con `AllowConcurrentExecutions = false`**: no existe en CommunityToolkit.Mvvm 8.4.0; requeriría actualizar toolkit o implementar custom command
- **DbContext pooling con múltiples instancias**: agrega complejidad innecesaria; el problema NO es performance sino evitar overlapping async ops sobre el mismo contexto
- **Cancelar operación previa al iniciar nueva**: requiere `CancellationTokenSource` por comando y lógica de cancel + reinicio; más complejo que el guard simple

**Razón**: El guard booleano es:
- **Simple**: 3 líneas (guard check, set, reset en finally)
- **Seguro**: `finally` garantiza que el guard se libera incluso si hay excepción
- **Predecible**: comportamiento idempotente (llamada durante refresh en curso = no-op silencioso)
- **Sin overhead**: sin locks, sin tasks adicionales, sin infraestructura nueva
- **Consistente**: patrón único aplicado uniformemente en todos los ViewModels operacionales
- **No bloquea UI**: `IsBusy` sigue manejando loading indicators; guard solo previene overlapping DbContext ops

**Impacto**:
- **ViewModels modificados** (13 total):
  - Inventario: `SalidasViewModel`, `EntradasViewModel`, `ExistenciasViewModel`, `KardexViewModel`, `ProductosViewModel`
  - Ventas: `CotizacionesViewModel`, `PedidosViewModel`, `OrdenesTrabajoViewModel`
  - Finanzas: `GastosViewModel`, `IngresosViewModel`, `CxCViewModel`, `CxPViewModel`
- **Patrón aplicado**:
  ```csharp
  private bool _isRefreshing;  // o _isLoading

  [RelayCommand]
  public async Task RefrescarAsync(CancellationToken ct = default)
  {
      if (_isRefreshing) return;  // ← Early return: evita reentrada
      if (Session.EmpresaId == 0) return;

      _isRefreshing = true;
      IsBusy = true;
      try { /* ... service call */ }
      finally
      {
          IsBusy = false;
          _isRefreshing = false;  // ← Siempre liberado
      }
  }
  ```
- **Build**: exitoso (0 errores)
- **Documentación**:
  - `docs/CLAUDE_RULES.md` §7: nueva sección "DbContext Runtime Concurrency Rules" con ejemplos CORRECTO vs INCORRECTO, lista de ViewModels protegidos, anti-patrones prohibidos
  - `docs/KNOWN_ISSUES.md`: nuevo issue `KI-013` [MITIGADO] con causa raíz, mitigación aplicada, ViewModels protegidos, regla preventiva
  - `docs/DECISIONS.md`: este ADR-020

**Decisión de scope**:
- **NO implementar framework complejo de locking**: innecesario, el guard simple resuelve el problema
- **NO cambiar arquitectura DI** (DbContext sigue scoped como debe ser)
- **NO agregar telemetría enterprise de concurrencia**: si la excepción ocurre, ya se detecta en runtime; no justifica infraestructura adicional
- **NO modificar Runtime Observability / Security Foundation / WorkspaceService**: estos servicios ya son DbContext-safe (singleton con snapshots, no usan DbContext directamente)

**Runtime estable**: Con los guards aplicados, el ERP soporta navegación multi-tab rápida, refresh concurrente, y comandos async sin riesgo de `InvalidOperationException` por concurrencia DbContext.

---

## ADR-019 — Operational Inventory Experience: trazabilidad documental runtime + navegación venta origen
- Usar EF projection inline con Include de User (causa N+1 a menos que se toque `AsSplitQuery`, mejor batch manual)
- Poner lógica ternaria de color directo en x:Bind XAML (syntax error en WinUI 3, mejor converter + computed property)
- Crear un dashboard BI complejo con queries agregadas (fuera de scope PYME light inicial)

**Razón**:
`Salida.VentaId` y `Entrada.ProveedorId` ya existen en dominio; solo faltaba proyectarlos en DTO y UI. El patrón de evento viewmodel→page code-behind→workspace es el mismo usado en `PedidoDocumentoPage` / `OrdenTrabajoDocumentoPage`, reutilizable sin refactorings. El batch lookup de usernames es eficiente y legible. El converter XAML es la práctica estándar WinUI 3 para binding con transformación. Dashboard light sin backend query permite entregar UX operacional sin bloquear por falta de KPIs complejos.

**Impacto**:
- `SalidaResumenDto`: +`long? VentaId`, +`string? UsuarioNombre` (parámetros opcionales, compatibles).
- `EntradaResumenDto`: +`string? ProveedorNombre`, +`string? UsuarioNombre` (parámetros opcionales).
- `ExistenciaDto`: +`string EstadoStock` (computed property: `"Normal"` | `"Bajo"` | `"Agotado"`).
- `SalidaService.ListarAsync`: batch lookup `_context.Users.ToDictionaryAsync(u => u.Id, u => u.UserName)`.
- `EntradaService.ListarAsync`: Include `Proveedor`, batch lookup usuarios.
- `SalidasViewModel`: +`TieneVentaOrigen` (CanExecute), +`AbrirVentaOrigenCommand`, +`event EventHandler<long>? VentaOrigenSolicitada`.
- `SalidasPage.xaml.cs`: suscripción evento→`OnVentaOrigenSolicitada`→`_workspace.OpenTab`→`VentaDocumentoPage`.
- `SalidasPage.xaml`: columnas `Venta #` y `Usuario`, botón "Abrir Venta Origen".
- `EntradasPage.xaml`: columnas `Proveedor` y `Usuario`.
- `ExistenciasPage.xaml`: columna `●` + `StockStateToColorConverter`.
- `InventarioPage.xaml`: dashboard light strip (Grid Row 0, placeholder para indicadores futuros).
- +`StockStateToColorConverter.cs` en `Ybridio.WinUI\Converters`.

**Decisión de scope**: NO implementar navegación a proveedor/OrdenCompra desde Entradas porque el módulo Compras UI no está completo. NO crear dashboard dinámico con queries de `IInventarioService` en este paso (futura extensión sin cambios arquitectónicos).

---

## ADR-018 — Inventory Operational Completion: KardexLineaDto + ListarKardexFiltradoAsync

**Decisión**: Completar el inventario operacional extendiendo el servicio y DTOs existentes (`IInventarioService`, `ExistenciaDto`, `MovimientoInventarioDto`) en lugar de crear un motor de inventario nuevo. Agregar `KardexLineaDto` enriquecido, `ListarKardexFiltradoAsync` con filtros multi-dimensionales, `PermisosClave.Kardex.Ver`, indicadores de stock bajo en `ExistenciasViewModel`, y `KardexViewModel` + `KardexPage` funcionales.

**Alternativas consideradas**:
- Crear un `IKardexService` separado (duplica lógica ya en `IInventarioService`)
- Motor de inventario enterprise con PEPS/UEPS/MRP (fuera del scope PYME explícito)
- Subconsulta EF para UsuarioNombre en proyección Kardex (causa N+1; mejor resolver en capa presentación si se necesita)

**Razón**:
`MovimientoInventario` ya almacenaba todos los campos necesarios (`SaldoAcumulado`, `ReferenciaId`, `Referencia`, `Observaciones`, `SucursalId`, `Folio`). Extender el servicio existente con un overload filtrado mantiene la lógica de seguridad (scope almacén, `kardex.ver`) centralizada. `ExistenciaDto` ya tenía suficiente para stock-bajo al agregar `StockMinimo` opcional con valor default. No se tocó el motor de Security Foundation ni WorkspaceService.

**Impacto**:
- `PermisosClave`: +`Kardex.Ver = "kardex.ver"`, agregado a `Todos()`.
- `ExistenciaDto`: +`StockMinimo?` (parámetro opcional, no rompe callers existentes).
- `KardexLineaDto`: DTO nuevo con Entrada/Salida split, SaldoAcumulado, trazabilidad documental.
- `IInventarioService`: +`ListarKardexFiltradoAsync(empresaId, productoId?, almacenId?, tipoMovId?, desde?, hasta?)`.
- `InventarioService`: implementación con enforcement `kardex.ver` + scope almacén.
- `KardexViewModel`: nuevo, con filtros, observabilidad, permiso, evento `DocumentoOrigenSolicitado`.
- `KardexPage.xaml/cs`: grid Outlook 2026 con filtros de fecha y producto, colores Entrada/Salida.
- `ExistenciasViewModel`: +`ConteoStockBajo`, `ConteoAgotados`, `AlertaStockVisible`.
- `ExistenciasPage.xaml`: indicador visual stock bajo/agotados en StatusBar.

---



## ADR-017 — Workflow Actions Layer como extensión directa de ViewModels existentes

**Decisión**: Implementar las acciones de workflow (Pedido→Venta, OT→Entregada, Navegación Cruzada Venta↔Pedido) como commands adicionales en los ViewModels documentales existentes, usando el patrón `Action<T>? Notify...` ya establecido en `CotizacionDocumentoViewModel` / `PedidoDocumentoViewModel`. No crear un motor de workflow separado.

**Alternativas consideradas**:
- Crear `IWorkflowService` o `WorkflowEngine` centralizado (overhead innecesario para PYME)
- BPM / estados configurables dinámicamente (complejidad enterprise, fuera de scope)
- Decorar ViewModels con un mediator de mensajes (añade dependencia, no aporta para el volumen esperado)

**Razón**:
Los flujos son simples y lineales. El patrón `Action<T>? NotificarXxx` ya probado en `PedidoDocumentoPage` (para OT) es suficiente: la Page asigna el callback, el ViewModel ejecuta la conversión via servicio Application, y el callback abre el tab en WorkspaceService. Añadir una capa de mediación solo agrega indirección sin beneficio observable.

**Impacto**:
- `PedidoDocumentoViewModel`: +`GenerarVentaAsync`, +`NotificarVentaGenerada`, +`PuedeGenerarVenta`, +`IVentaDocumentalService`.
- `OrdenTrabajoDocumentoViewModel`: +`MarcarEntregadaAsync`, +`PuedeMarcarEntregada`.
- `VentaDocumentoViewModel`: +`PedidoOrigenId`, +`TienePedidoOrigen`.
- `PedidoDocumentoPage`: +`BtnGenerarVenta_Click`, +`AbrirVentaEnWorkspace`.
- `OrdenTrabajoDocumentoPage`: +`BtnMarcarEntregada_Click`.
- `VentaDocumentoPage`: +`BtnAbrirPedidoOrigen_Click`, +`IWorkspaceService`, +`IPedidoService`.
- Toda la lógica de conversión reutiliza `IVentaDocumentalService.GenerarDesdePedidoAsync` y `IOrdenTrabajoService.CambiarEstatusAsync` ya existentes.

---

## ADR-016 — Sales Transaction Layer paralelo al POS legacy

**Decisión**: Crear `IVentaDocumentalService` / `VentaDocumentalService` como capa documental nueva, dejando el POS legacy (`IVentaService`) intacto.

**Alternativas consideradas**:
- Reescribir `VentaService` para soportar flujo documental (riesgo de romper POS)
- Usar `IVentaService` con flags booleanos de modo (acoplamiento excesivo)

**Razón**:
El POS legacy maneja tiempos reales de caja y permisos distintos. El flujo documental PYME requiere confirmación explícita, manejo de crédito/contado, descuento de inventario diferido y CxC automática. Separar los contratos evita riesgo de regresión y permite evolucionar ambos flujos de forma independiente.

**Impacto**:
- `Venta.cs` extendida con campos opcionales compatibles con POS (`ClienteId`, `Estatus`, `TipoPago`, `Pagos`).
- `PagoVenta` como entidad nueva en `ventas.PagoVenta`.
- `ConfirmarAsync` descuenta inventario vía `MovimientoInventario` y crea `CuentaPorCobrar` si `TipoPago = Credito`.
- Fórmulas runtime: `Importe = Cantidad × PrecioUnitario`; `Total = SUM(detalles)`; `SaldoPendiente = Total - TotalPagado`.
- Script DDL incremental: `scripts/ventas_transaction_layer.sql` (sin migrations automáticas, alineado con ADR-004).

---

## ADR-001 — Clean Architecture en 4 capas

**Decisión**: Domain → Infrastructure → Application → WinUI

**Alternativas consideradas**:
- Arquitectura en 2 capas (UI + acceso directo a datos)
- CQRS con MediatR

**Razón**:
El negocio requiere lógica reutilizable entre POS, módulo administrativo y futuras apps móviles/web. La separación en 4 capas permite cambiar la presentación sin tocar lógica de negocio. CQRS fue descartado por overhead innecesario en un ERP PYME.

**Impacto**: Todos los servicios viven en Application, los ViewModels son orquestradores, la BD solo es alcanzable desde Infrastructure.

---

## ADR-002 — ASP.NET Core Identity adaptado (no reemplazado)

**Decisión**: Extender `IdentityUser<Guid>` y `IdentityRole<Guid>` con propiedades de negocio (EmpresaId, Nombre, Activo, Borrado).

**Alternativas consideradas**:
- Sistema de autenticación custom desde cero
- OpenIddict / Duende IdentityServer

**Razón**:
Identity ya resuelve hash de contraseñas, claims, tokens y roles. Reimplementarlo es riesgo de seguridad. OpenIddict agrega complejidad innecesaria para un desktop app. La extensión mantiene compatibilidad y agrega las propiedades de negocio.

**Impacto**: `ApplicationUser` y `ApplicationRole` en `seguridad.*` schema. ErpDbContext hereda de IdentityDbContext.

---

## ADR-003 — Un solo DbContext, filtros globales automáticos

**Decisión**: `ErpDbContext` único con filtros de soft-delete y multi-tenancy aplicados via reflection en `OnModelCreating`.

**Alternativas consideradas**:
- DbContext separado por módulo
- Filtros manuales en cada query

**Razón**:
Múltiples DbContexts generan problemas de transacciones cruzadas (ej: Venta → descuenta inventario → registra movimiento de caja). Los filtros globales eliminan el riesgo de olvidar `!Borrado` o el `EmpresaId` en cualquier query.

**Impacto**: Toda entidad con `Borrado` y `EmpresaId` queda automáticamente filtrada. El bypass (`EmpresaId == 0` para tooling) está documentado.

---

## ADR-004 — ServiceResult<T> sin excepciones de negocio

**Decisión**: Todos los métodos de escritura retornan `ServiceResult<T>` o `ServiceResult`. Las excepciones solo son para errores de infraestructura inesperados.

**Alternativas consideradas**:
- Lanzar excepciones de dominio (DomainException)
- Fluent Validation con throw

**Razón**:
Las excepciones tienen costo de performance y hacen el flujo difícil de seguir en la UI. `ServiceResult` permite que el ViewModel decida qué mostrar al usuario sin parsear mensajes de error. El `ErrorCode` enum permite reacciones específicas (e.g., `Unauthorized` → mostrar mensaje diferente que `NotFound`).

**Impacto**: Patrón obligatorio en todos los servicios. ViewModels checan `result.Success` antes de actuar.

---

## ADR-005 — RBAC + Profiles con evaluación en 3 niveles

**Decisión**: Los permisos se resuelven en orden: UsuarioPermiso (override) → PerfilPermiso → RolPermiso. Un denegado explícito veta todos los niveles.

**Alternativas consideradas**:
- Solo roles (sin perfiles ni overrides)
- Permisos flat por usuario (sin herencia)

**Razón**:
Solo roles limita la flexibilidad cuando un usuario necesita permisos extra sin cambiar su rol. Los perfiles reutilizables resuelven asignaciones frecuentes (ej: "POS Básico"). Los overrides permiten excepciones puntuales sin crear roles ad-hoc.

**Impacto**: `PermisoService` evalúa los 3 niveles. `MemoryPermissionCache` (TTL 10 min) evita N+1 queries en cada `PuedeAsync`.

---

## ADR-006 — PermisosClave como constantes tipadas (no enum)

**Decisión**: Clase estática `PermisosClave` con subclases por módulo y constantes `string`.

**Alternativas consideradas**:
- Enum de permisos
- Strings directo en código

**Razón**:
Los enums no se serializan bien a `string` sin conversores y complican la seed de BD. Los strings literales generan typos silenciosos. Las constantes tipadas dan autocompletado, son refactorizables y coinciden exactamente con los valores en BD (`entidad.accion` minúsculas).

**Impacto**: Todo `PuedeAsync(...)` debe usar `PermisosClave.*`. La regla es obligatoria y verificable con grep.

---

## ADR-007 — SQL DDL directo (sin migraciones EF)

**Decisión**: Los cambios de esquema se aplican con scripts `.sql` ejecutados via `sqlcmd.exe`. EF Core solo se usa como ORM, no como herramienta de migración.

**Alternativas consideradas**:
- EF Core Migrations
- DbUp / Flyway

**Razón**:
Las migraciones EF generan archivos difíciles de auditar y no funcionan bien con esquemas SQL Server con múltiples schemas nombrados. Los scripts SQL son legibles, versionables en git y permiten control total del DDL. El cliente tiene acceso directo al SQL Server y prefiere este approach.

**Impacto**: Todo cambio de esquema requiere: (1) script SQL en `scripts/`, (2) entidad Domain, (3) EF Configuration, (4) DbSet en ErpDbContext.

---

## ADR-008 — WorkspaceService: dos capas de contenido en el Shell

**Decisión**: `ShellPage` tiene dos capas: `ModuleFrame` (módulo principal, siempre renderizado) y `WorkspaceTabView` (tabs persistentes, superpuesto).

**Alternativas consideradas**:
- Un solo Frame con navegación de pila
- Tabs solo para el workspace, sin ModuleFrame

**Razón**:
El ModuleFrame permite que módulos como Dashboard, POS e Inventario tengan su propio TabView interno sin conflictos. El WorkspaceService agrega tabs de trabajo (Productos específicos, comparaciones) que persisten al cambiar de módulo, similar a VS Code.

**Impacto**: Los módulos que viven en ModuleFrame tienen un Frame propio. Los ítems de workspace tienen su propio ciclo de vida.

---

## ADR-009 — Lazy-loading de tabs en páginas de módulo

**Decisión**: Las páginas con TabView (InventarioPage, FinanzasPage, ConfiguracionPage) cargan cada tab en su Frame solo la primera vez que se activa.

**Alternativas consideradas**:
- Cargar todos los tabs al navegar al módulo
- Recargar el tab en cada selección

**Razón**:
Cargar todo al inicio degrada el tiempo de navegación inicial. Recargar en cada selección destruye el estado (búsqueda activa, ítem seleccionado). La carga lazy con flags booleanos (`_entradasLoaded`) mantiene el estado y carga bajo demanda.

**Impacto**: Patrón `ILiveContextReporter` para que tabs ya cargados actualicen el contexto de observabilidad sin recargar datos.

---

## ADR-010 — Finanzas Operativas: NO contabilidad doble

**Decisión**: El módulo Finanzas registra movimientos simples (gasto/ingreso) sin contrapartidas contables.

**Alternativas consideradas**:
- Sistema contable con partida doble
- Pólizas contables con catálogo de cuentas

**Razón**:
El target es PYME comercial que necesita control del flujo de efectivo operativo, no cumplimiento contable formal. La contabilidad doble agrega complejidad (plan de cuentas, pólizas, balanzas, SAT) que está fuera del alcance del producto. Si el cliente necesita contabilidad formal, usará un ERP dedicado (SAP, CONTPAQi) o solicitará la integración explícitamente.

**Impacto**: `MovimientoFinanciero` tiene Concepto, Monto, Fecha, Categoría. No hay Must-Have de contrapartida. `ContextoFinanciero` permite future-proof para finanzas personales.

---

## ADR-011 — Doble capa de enforcement de autorización

**Decisión**: El permiso se verifica en el ViewModel (pre-check UX) Y en el Service (defensa en profundidad).

**Alternativas consideradas**:
- Solo en el Service
- Solo en el ViewModel

**Razón**:
Solo en el Service: el usuario espera el resultado de la query antes de ver el mensaje de error. Solo en el ViewModel: si el Service se llama desde otro punto (futuro API, otra VM), la validación se saltaría. La doble capa garantiza UX rápida y seguridad real.

**Impacto**: Todos los ViewModels con datos sensibles tienen el pre-check. Todos los servicios con write ops tienen el guard. El patrón está en CLAUDE_RULES.md como obligatorio.

---

## ADR-012 — ContentDialog para CRUD en lugar de ventanas separadas

**Decisión**: Los formularios de creación/edición simples usan `ContentDialog` inline en la Page. Solo los formularios complejos (UsuarioDetailWindow) usan ventanas separadas.

**Alternativas consideradas**:
- Ventana secundaria para todos los formularios
- Panel lateral (flyout/panel) inline

**Razón**:
ContentDialog es el patrón WinUI estándar para formularios breves. Las ventanas separadas tienen más overhead (registro, gestión de lifecycle). Los formularios de Gastos, Ingresos, CxC/CxP tienen 4-6 campos — caben cómodamente en un ContentDialog.

**Impacto**: ContentDialogs requieren `XamlRoot` de la Page → los ViewModels exponen callbacks `Action<T>?` en lugar de crear los diálogos directamente.

---

## ADR-013 — Soft-delete universal (sin eliminación física)

**Decisión**: Ninguna entidad se elimina físicamente. Se marca `Borrado = true` y el filtro global la excluye automáticamente.

**Alternativas consideradas**:
- Eliminación física con archive table
- Columna `DeletedAt` datetime (en lugar de bool)

**Razón**:
La eliminación física rompe integridad referencial y destruye historial de auditoría. El bool `Borrado` con filtro global es simple, eficiente y reversible. Un `datetime` añade info útil pero el `FechaModificacion + UsuarioModificacionId` ya captura cuándo y quién borró.

**Impacto**: El filtro global `!Borrado` aplica a todas las entidades de tipo `AuditableEntity` y `CreationAuditEntity`. Las queries administrativas que necesitan ver borrados deben usar `.IgnoreQueryFilters()`.

---

## ADR-014 — `_context.Roles` para queries de roles en PermisoService

**Decisión**: En `PermisoService`, los JOINs con la tabla de roles usan `_context.Roles` (que expone `DbSet<ApplicationRole>`), nunca `_context.Set<IdentityRole<Guid>>()`.

**Problema detectado**: El código original usaba `_context.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()`. Esto causaba excepción en runtime:
```
InvalidOperationException: Cannot create a DbSet for 'IdentityRole<Guid>'
because this type is not included in the model for the context.
```

**Razón**: `ErpDbContext` hereda de `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`. EF Core registra `ApplicationRole` (el tipo derivado con campos custom), NO el tipo base genérico `IdentityRole<Guid>`. Intentar acceder al tipo base falla porque no existe en el modelo EF.

**Alternativas descartadas**:
- Usar `_context.Set<ApplicationRole>()` — funciona pero `_context.Roles` es más legible y semántico
- Cambiar el JOIN por un `Contains(roleId)` en memoria — ineficiente con muchos roles

**Impacto**: Toda la evaluación de permisos runtime (Nivel 3 — herencia de roles) fallaba silenciosamente o con excepción. El fix restableció el Security Foundation completo.

**Regla preventiva**: En cualquier query que necesite acceder a roles, usar siempre `_context.Roles`. Ver `PermisoService.cs` como referencia.

---

## ADR-015 — DateTimeOffset wrappers para DatePicker en WinUI 3

**Decisión**: Los ViewModels de documento (Cotizacion, Pedido, OrdenTrabajo) mantienen propiedades `DateTime`/`DateTime?` internamente, y exponen wrappers `DateTimeOffset` para binding con `DatePicker.Date`.

**Problema detectado**: `DatePicker.Date` en WinUI 3 es `DateTimeOffset`, no `DateTime`. El binding directo causaba error XAML en compilación:
```
Invalid binding assignment: Cannot directly bind type 'System.DateTime' to 'System.DateTimeOffset'.
```

**Solución implementada**:
```csharp
// Propiedad interna (usada por servicios que esperan DateTime)
[ObservableProperty] private DateTime fecha = DateTime.Today;

// Wrapper para DatePicker (expuesto al XAML)
public DateTimeOffset FechaOffset
{
    get => new DateTimeOffset(Fecha);
    set => Fecha = value.DateTime;
}
// Notificación cruzada:
partial void OnFechaChanged(DateTime value) => OnPropertyChanged(nameof(FechaOffset));
```

**Por qué no cambiar las propiedades internas a `DateTimeOffset`**: Los DTOs de Application layer usan `DateTime`. Mantener `DateTime` internamente evita conversiones en los services y DTOs, que son la fuente de verdad. La UI adapta con el wrapper.

**Impacto**: DatePicker funciona correctamente en todas las páginas de documento. El patrón es reutilizable para cualquier ViewModel que necesite DatePicker en WinUI 3.

---

## ADR-021 — Workspace Operational UX Stabilization: single-instance policy & centralized open-or-activate pattern

**Decisión**: Aplicar un patrón **single-document-instance** en el Workspace: un solo tab por documento operacional (Venta, Pedido, OT, Cliente, Producto, etc.). Centralizar el patrón `Exists() → ActivateTab()` vs `await service → OpenTab()` que antes se repetía manualmente en múltiples páginas mediante un helper opt-in `IWorkspaceService.OpenOrActivateDocumentTabAsync<TData>`.

**Problema identificado**: Caos operacional en tabs/documentos del ERP:
- **Tabs duplicados**: el mismo documento (e.g., Venta #91) podía abrirse múltiples veces si el usuario hacía clic repetido o navegaba desde diferentes workflows
- **Foco inconsistente**: workflows abrían documentos pero no activaban el tab existente (quedaban en background invisibles)
- **Lógica repetitiva**: cada Page repetía el patrón manual `if (_workspace.Exists(key)) { _workspace.ActivateTab(key); return; } else { await service; _workspace.OpenTab(...); }`
- **Keys/titles inconsistentes**: no había convención formal documentada para claves de tabs (`venta-{id}`, `pedido-{id}`) ni títulos runtime (`Venta #91`, `Pedido #55`)
- **Contexto perdido**: sin deduplicación clara, el usuario podía perder contexto operacional navegando entre múltiples instancias del mismo documento

**Alternativas consideradas**:
- **Rehacer WorkspaceService / Shell completo**: overhead innecesario; el núcleo de `WorkspaceService.OpenTab()` ya deduplicaba por `key`, solo faltaba centralizar el patrón de llamada
- **Implementar dock manager enterprise / MDI complejo**: complejidad excesiva para el problema; el ERP necesita estabilización UX, no arquitectura desktop avanzada
- **Forzar single-tab global (solo un documento abierto a la vez)**: demasiado restrictivo; rompe workflows multi-documento (e.g., Cotización → Pedido → Venta)
- **Tab groups / workspace manager adicional**: overhead arquitectónico innecesario; el problema es operacional (deduplicación/activación), no estructural

**Razón**: El helper `OpenOrActivateDocumentTabAsync<TData>` es:
- **Conservador**: NO reescribe `WorkspaceService` ni Shell; solo agrega un método conveniente opt-in
- **Centralizado**: elimina código repetitivo de 3+ páginas (SalidasPage, PedidosPage, OrdenesTrabajoPage) y estandariza el patrón
- **Single-instance automático**: si el documento ya existe, activa el tab; si no existe, carga datos async y abre nuevo tab
- **Activación garantizada**: siempre llama `ActivateTab(key)` (nuevo o existente), evitando tabs invisible en background
- **Error-handling consistente**: callback `onError` opcional permite manejar errores de carga sin duplicar lógica `try/catch` en cada Page
- **Context-preserving**: reutiliza `WorkspaceTabItem` existente con su `Page` viva, preservando filtros/scroll/selección
- **Documentado**: convenciones formales de key (`{tipo}-{id}`, `{tipo}-nueva-{guid}`, `{modulo}`) y title (`{Tipo} #{id}`, `Nuevo/Nueva {Tipo}`, nombre módulo)

**Impacto**:
- **Archivos modificados**:
  - `Ybridio.WinUI\Services\Workspace\IWorkspaceService.cs`: agregado contrato `Task<WorkspaceTabItem?> OpenOrActivateDocumentTabAsync<TData>(...)`
  - `Ybridio.WinUI\Services\Workspace\WorkspaceService.cs`: implementado helper con lógica `Exists() → ActivateTab()` vs `await dataLoader() → OpenTab()`
  - `Ybridio.WinUI\Views\Inventario\SalidasPage.xaml.cs`: refactorizado `OnVentaOrigenSolicitada` para usar helper
  - `Ybridio.WinUI\Views\Ventas\PedidosPage.xaml.cs`: refactorizado `AbrirPedidoEnWorkspace` para usar helper
  - `Ybridio.WinUI\Views\Ventas\OrdenesTrabajoPage.xaml.cs`: refactorizado `AbrirOTEnWorkspace` para usar helper

- **Patrón aplicado** (ejemplo):
  ```csharp
  await _workspace.OpenOrActivateDocumentTabAsync(
      key:         $"venta-{ventaId}",
      title:       $"Venta #{ventaId}",
      icon:        "",
      dataLoader:  () => _ventaService.ObtenerConDetallesAsync(ventaId)
                          .ContinueWith(t => t.Result.Success ? t.Result.Value : null),
      pageFactory: dto => new VentaDocumentoPage(dto!),
      onError:     err => ViewModel.ErrorMessage = err,
      isClosable:  true);
  ```

- **Convenciones formalizadas** (documentadas en `docs/CLAUDE_RULES.md` sección 15):
  - **Key formats**: documentos guardados `{tipo}-{id}` (e.g., `venta-91`), documentos nuevos `{tipo}-nueva-{guid}`, módulos `{modulo}` (e.g., `inventario`)
  - **Title formats**: documentos guardados `{Tipo} #{id}` (e.g., `Venta #91`), documentos nuevos `Nuevo/Nueva {Tipo}`, módulos nombre completo
  - **Single-instance policy**: un solo tab por documento operacional; si ya existe, activar en lugar de duplicar
  - **Tab activation**: workflows abren/activan automáticamente el tab; no dejar tabs en background invisible
  - **Context preservation**: `WorkspaceTabItem.Content` mantiene `Page` viva para preservar estado ViewModel/filtros/selección

- **Build**: exitoso (0 errores)

- **Documentación actualizada**:
  - `docs/CLAUDE_RULES.md`: nueva sección 15 "Workspace Operational UX Stabilization" con reglas single-instance, convenciones key/title, workflow de apertura, anti-patterns, performance, Workspace vs WindowManager
  - `docs/DECISIONS.md`: ADR-021 (este documento)

**UX esperado post-estabilización**:
- Usuario abre Venta #91 desde Salidas → tab se abre y activa
- Usuario intenta abrir Venta #91 de nuevo desde otro workflow → tab existente se activa (no duplicado)
- Usuario navega Cotización #10 → Pedido #55 → Venta #91 → tabs quedan ordenados y contexto preservado
- Usuario cierra tab activo → tab vecino se activa automáticamente
- Workspace se siente estable, ordenado, predecible, ERP-like (no browser tabs caótico)

**Anti-patterns evitados**:
- ❌ Tabs duplicados del mismo documento
- ❌ Tabs abiertos sin foco (invisible en background)
- ❌ Código repetitivo `Exists() / ActivateTab() / OpenTab()` en múltiples páginas
- ❌ Keys/titles ambiguos o inconsistentes
- ❌ Pérdida de contexto runtime al navegar entre tabs

**Notas adicionales**:
- El helper es **opt-in**: páginas que no necesitan deduplicación (e.g., documentos nuevos con GUID único) pueden seguir usando `OpenTab()` directamente
- `WorkspaceService` NO maneja lógica de negocio; solo coordinación de navegación/activación/deduplicación runtime
- El patrón es compatible con Runtime Observability: `ActiveTab`, tabs reutilizados, navegación workflow, contexto operacional visible en Diagnostic Panel
- Performance: sin overhead; activación inmediata; estado preservado (no reload innecesario)

---
