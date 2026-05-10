# Ybridio ERP — Master Architecture Rules for Claude Code

> Estas reglas aplican para TODOS los requerimientos futuros del proyecto.  
> Claude Code debe leer y respetar este documento ANTES de implementar cualquier cambio.  
> Estas reglas son **permanentes** y forman parte oficial de la arquitectura del ERP.

---

## 1. Arquitectura General

El proyecto utiliza arquitectura modular por capas:

- Domain
- Application
- Infrastructure
- WinUI

**Regla obligatoria**: RESPETAR completamente la separación de capas.

---

## 2. Responsabilidades por Capa

### Domain
**Contiene**: entidades, reglas de dominio, enums, contratos de dominio, lógica pura de negocio.  
**NO**: EF Core, WinUI, SQL, XAML, dependencias UI.

### Application
**Contiene**: casos de uso, DTOs, interfaces de servicios, autorización, validaciones de aplicación, lógica operacional.  
**NO**: XAML, controles WinUI, lógica visual.

### Infrastructure
**Contiene**: EF Core, DbContext, configuraciones, Identity, repositorios, queries SQL, servicios externos, caches.  
**NO**: lógica visual, XAML, navegación WinUI.

### WinUI
**Contiene**: Views, ViewModels, navegación, bindings, grids, command bars, experiencia de usuario.  
**NO**: lógica de negocio compleja, autorización central, queries SQL directos.

---

## 3. Regla Crítica — NO Rehacer

**Nunca rehacer**:

- Security Foundation
- Runtime Observability
- WorkspaceService
- SessionService
- Navegación principal / Shell
- Arquitectura existente
- Runtime Diagnostic Panel
- DbContext
- Identity

---

## 4. Regla Crítica — NO Modificar de Más

Claude debe:
- Modificar **únicamente lo necesario**
- Mantener compatibilidad existente
- Evitar refactors innecesarios
- Evitar mover archivos innecesariamente
- Evitar renombrar componentes sin necesidad explícita del usuario

---

## 5. Reutilización Obligatoria

**Antes de crear** servicios, DTOs, entidades, helpers o componentes, Claude debe analizar:
- Qué ya existe
- Qué puede reutilizarse
- Qué puede extenderse

**No duplicar funcionalidades existentes.**

---

## 6. Seguridad

La seguridad utiliza: ASP.NET Identity, RBAC, Profiles, Runtime Authorization, Security Scopes.

### Regla crítica

```csharp
// ❌ NUNCA
if (rol == "Admin") { }

// ✅ SIEMPRE
if (await _auth.PuedeAsync(PermisosClave.Venta.Crear, ct)) { }
```

Usar **siempre**: `IErpAuthorizationService`, `ISecurityScopeResolver`, `PermisosClave`, Runtime Security.

**Los permisos SIEMPRE son DATA. NO hardcodear permisos.**

### Doble capa obligatoria
```csharp
// ViewModel (pre-check UX rápida)
if (!await _auth.PuedeAsync(PermisosClave.Entrada.Ver, ct))
{ ErrorMessage = "Sin permiso..."; return; }

// Service (defensa en profundidad)
if (!await _auth.PuedeAsync(PermisosClave.Entrada.Ver, ct))
    return ServiceResult<T>.Fail("Sin permiso...", ErrorCode.Unauthorized);
```

### Invalidar caché tras cambios de roles/permisos
```csharp
// Cuando se modifican permisos de un perfil o se reasigna un perfil a un usuario
// la caché (MemoryPermissionCache con TTL 10 min) se invalida automáticamente
// al siguiente request, o bien se puede invalidar manualmente si existe el método.
```

---

## 7. DbContext Runtime Concurrency Rules

**DbContext NO es thread-safe.**  
**DbContext scoped NO puede ejecutar múltiples operaciones concurrentes.**

### Problema principal

```csharp
// ❌ INCORRECTO — causa System.InvalidOperationException:
// "A second operation was started on this context instance before a previous operation completed."

// Usuario hace clic en "Refrescar" mientras hay un refresh en curso
await RefrescarAsync();  // Primera operación
await RefrescarAsync();  // Segunda operación simultánea → EXCEPCIÓN
```

### Contextos de riesgo

- **Navegación rápida**: `OnNavigatedTo` puede llamar a `LoadAsync/RefrescarAsync` antes de que termine una carga previa.
- **Timers**: `DispatcherTimer` puede disparar refresh mientras otro está en curso.
- **Comandos concurrentes**: Usuario hace clic repetidamente o usa atajos de teclado.
- **Observabilidad runtime**: Panels de diagnóstico que consultan el contexto.
- **Multi-tabs**: Múltiples módulos abiertos simultáneamente.

### Patrón obligatorio: Single-Flight Guard

Todos los ViewModels con `LoadAsync` / `RefrescarAsync` DEBEN usar un guard booleano:

```csharp
// ✅ CORRECTO — patrón single-flight

private bool _isRefreshing;  // o _isLoading

[RelayCommand]
public async Task RefrescarAsync(CancellationToken ct = default)
{
    if (_isRefreshing) return;  // ← Guard: evita reentrada
    if (Session.EmpresaId == 0) return;

    _isRefreshing = true;  // ← Bloquea ejecución concurrente
    IsBusy = true;
    ErrorMessage = string.Empty;
    var sw = Stopwatch.StartNew();

    try
    {
        // ... lógica de carga con DbContext
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }
    finally
    {
        IsBusy = false;
        _isRefreshing = false;  // ← Siempre liberar en finally
    }
}
```

### Reglas de aplicación

1. **Un guard por comando async que use DbContext** (directo o indirectamente vía Application service).
2. **Guard al inicio del comando**, antes de cualquier `await`.
3. **Early return si guard activo**, sin logging ni side effects.
4. **Liberar guard en `finally`**, nunca en el `try` o `catch`.
5. **`IsBusy` es distinto del guard**: `IsBusy` controla UI, guard controla concurrencia DbContext.

### ViewModels con guards aplicados

**Inventario**:
- `SalidasViewModel.RefrescarAsync` → `_isRefreshing`
- `EntradasViewModel.RefrescarAsync` → `_isRefreshing`
- `ExistenciasViewModel.LoadAsync` → `_isLoading`
- `KardexViewModel.LoadAsync` → `_isLoading`
- `ProductosViewModel.RefrescarAsync` → `_isRefreshing`

**Ventas**:
- `CotizacionesViewModel.RefrescarAsync` → `_isRefreshing`
- `PedidosViewModel.RefrescarAsync` → `_isRefreshing`
- `OrdenesTrabajoViewModel.RefrescarAsync` → `_isRefreshing`

**Finanzas**:
- `GastosViewModel.RefrescarAsync` → `_isRefreshing`
- `IngresosViewModel.RefrescarAsync` → `_isRefreshing`
- `CxCViewModel.RefrescarAsync` → `_isRefreshing`
- `CxPViewModel.RefrescarAsync` → `_isRefreshing`

### Anti-patrones prohibidos

```csharp
// ❌ NO usar lock — ViewModels en UI thread, locks innecesarios
lock (_lock) { await _service.ListarAsync(...); }

// ❌ NO capturar DbContext en singleton
public class MySingletonService
{
    private readonly ErpDbContext _context; // ← PROHIBIDO
}

// ❌ NO Task.Run sin control — puede crear race conditions
Task.Run(() => await _service.ListarAsync(...));  // ← Peligroso

// ❌ NO fire-and-forget async
_ = RefrescarAsync();  // ← Sin await, sin control
```

### Observabilidad runtime: cómo es DbContext-safe

Los servicios de observabilidad (`RuntimeDiagnosticService`, `OperationalObservabilityService`, `CurrentContextTracker`) son **singleton** pero **NO usan DbContext**.  

Sólo mantienen snapshots en memoria (`RuntimeContextSnapshot`, `GridOperationContext`, `CurrentOperationalContext`).  
Los ViewModels **reportan** contexto **después** de completar la operación DbContext, no durante.

```csharp
// ✅ CORRECTO — reporte DESPUÉS de la query
var result = await _service.ListarAsync(Session.EmpresaId, ct);
sw.Stop();
_observability.Report(BuildOperationalContext(sw.Elapsed));  // ← Safe: solo metadata
_contextTracker.SetViewModelContext(BuildCurrentContext());
```

### Timers y refresh automático

Si un ViewModel necesita refresh automático (ej. `DiagnosticPanelViewModel` cada 2 segundos):

```csharp
// ✅ CORRECTO — timer llama a método idempotente con guard interno
_timer.Tick += (_, _) => Refresh();

private void Refresh()
{
    // GetSnapshot() es safe: solo lee snapshots en memoria, no usa DbContext
    _s = _diagnostic.GetSnapshot();
    OnPropertyChanged(string.Empty);
}
```

**NO usar timers que llamen a métodos async con DbContext.**  
Si es necesario, aplicar el mismo guard single-flight.

### Detección runtime de concurrencia

Si ocurre `System.InvalidOperationException: "A second operation was started on this context instance..."`:

1. **Registrar en logs** (si se implementa logging estructurado):
   - ViewModel donde ocurrió
   - Operación (LoadAsync / RefrescarAsync)
   - Timestamp
   - Stack trace

2. **Verificar que el guard existe** y está correctamente aplicado.

3. **Verificar que el guard se libera en `finally`**, no antes.

---

## 8. Observabilidad y diagnóstico
await _cache.InvalidateAsync(userId);
```

### Regla crítica: acceso a roles en EF — SIEMPRE `_context.Roles`

```csharp
// ✅ CORRECTO — ApplicationRole está registrado en el modelo
_context.Roles.Where(r => rolesUsuario.Contains(r.Name!))

// ❌ INCORRECTO — IdentityRole<Guid> NO está registrado, lanza InvalidOperationException
_context.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
```

`ErpDbContext` hereda de `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`. EF Core registra `ApplicationRole`, no el tipo base genérico. Ver ADR-014.

### Regla crítica: DatePicker en WinUI 3 requiere `DateTimeOffset`

`DatePicker.Date` es `DateTimeOffset`. Usar wrapper properties en el ViewModel:

```csharp
[ObservableProperty] private DateTime fecha = DateTime.Today;

public DateTimeOffset FechaOffset
{
    get => new DateTimeOffset(Fecha);
    set => Fecha = value.DateTime;
}
partial void OnFechaChanged(DateTime value) => OnPropertyChanged(nameof(FechaOffset));
```

El XAML bind a `FechaOffset`. El service recibe `Fecha` (DateTime). Ver ADR-015.

---

## 7. Multiempresa / Multisucursal / Multialmacén

El ERP es multiempresa, multisucursal y multialmacén.

Toda nueva funcionalidad debe considerar `EmpresaId`, `SucursalId`, `AlmacenId` y Security Scopes cuando aplique.

Los filtros globales del `ErpDbContext` aplican `!Borrado` y `EmpresaId` automáticamente a todas las entidades. **No romper este mecanismo.**

---

## 8. Runtime Observability

El ERP cuenta con: Runtime Diagnostic Panel, Operational Observability, Security Observability.

**No rehacer la observabilidad.**

Toda nueva operación importante debe integrarse cuando tenga sentido con:
- `IOperationalObservabilityService.Report(GridOperationContext)` tras cada carga/refresh
- `ICurrentContextTracker.SetViewModelContext(CurrentOperationalContext)` al activarse
- `ILiveContextReporter` si el ViewModel vive dentro de un TabView
- Nota `"ACCESO DENEGADO — permiso: x.y"` cuando el acceso falla

```csharp
_observability.Report(new GridOperationContext(
    Module: "Módulo", SubModule: "Sub",
    RecordCount: Items.Count, Duration: sw.Elapsed,
    Notes: denied ? [$"ACCESO DENEGADO — permiso: {permiso}"] : [$"scope info"]
));
```

---

## 9. WorkspaceService

- WorkspaceService **NO es** navegación principal.
- WorkspaceService **SÍ es** persistencia de documentos/workspaces.
- **No convertir** todos los módulos en WorkspaceItems.

Usar WorkspaceService **solamente** cuando exista documento persistente, edición persistente o multitarea real.

---

## 10. UI/UX Standards

El ERP utiliza: diseño Outlook 2026, estilo gris enterprise, tabs horizontales, command bars contextuales, grids Outlook-style, iconografía flat, tipografía consistente.

**No inventar nuevo diseño visual. No romper consistencia visual del ERP.**

### Recursos WinUI 3 obligatorios

| ❌ UWP (rompe en WinUI 3) | ✅ WinUI 3 |
|---|---|
| `SystemControlBackgroundChromeLowBrush` | `LayerFillColorDefaultBrush` |
| `SystemControlHighlightAccentBrush` | `AccentFillColorDefaultBrush` |
| `SystemControlForegroundBaseMediumBrush` | `TextFillColorSecondaryBrush` |

### Thickness: siempre 4 argumentos
```csharp
new Thickness(8, 4, 8, 4)  // ✓
new Thickness(8)            // ✗ no existe en WinUI 3
```

### ViewModel antes de InitializeComponent
```csharp
public MyPage()
{
    ViewModel = App.Services.GetRequiredService<MyViewModel>(); // PRIMERO
    InitializeComponent(); // DESPUÉS
}
```

### ContentDialog: siempre con XamlRoot
Los `ContentDialog` requieren `XamlRoot = XamlRoot` desde la Page. Los callbacks `Action<T>?` en ViewModels son el patrón establecido para abrir diálogos desde la Page.

---

## 11. Grid Standards

Todos los grids deben usar: buscador, contador de registros, virtualización, filtros estándar, diseño consistente.

**Filtros temporales estándar**: Hoy · 7 días · 30 días · 90 días · 6 meses · 1 año · Todo.

**Contenedor estándar**:
```xaml
<Border Margin="20,8,20,0" Background="White" BorderBrush="#E5E5E5" BorderThickness="1">
```

---

## 12. Command Bars

Las command bars deben: ser context-aware, reutilizar el estándar ERP, mantener agrupación consistente.

**No crear ribbons complejos innecesarios.**

---

## 13. SQL Server — Sin Migraciones EF

**No usar** migraciones automáticas de EF Core.

**Usar**: SQL directo, PowerShell, `sqlcmd.exe`.

Todo cambio de esquema requiere:
1. Script `.sql` en `scripts/`
2. Entidad en `Ybridio.Domain`
3. `IEntityTypeConfiguration<T>` en `Ybridio.Infrastructure/Persistence/Configurations/`
4. `DbSet` en `ErpDbContext`

**Precisión decimal obligatoria**:
- Montos/precios: `decimal(18,2)` → `HasColumnType("decimal(18,2)")`
- Cantidades/pesos: `decimal(18,6)` → `HasColumnType("decimal(18,6)")`

---

## 14. Naming Standards

Mantener: nomenclaturas en español, coherencia DbContext/SQL/entidades.

**No mezclar inglés y español en nombres de dominio.**

| Elemento | Regla | Ejemplo |
|---|---|---|
| Entidades | PascalCase español | `MovimientoFinanciero` |
| Servicios | `IXxxService` + `XxxService` | `IFinanzasService` |
| DTOs | `sealed record XxxDto` | `MovimientoFinancieroDto` |
| ViewModels | `sealed partial class XxxViewModel` | `GastosViewModel` |
| Métodos async | sufijo `Async` | `ListarAsync`, `CrearAsync` |
| Campos privados | `_camelCase` | `_service`, `_auth` |
| Claves de permisos | `entidad.accion` minúsculas | `finanzas.crear` |

**Lifetimes DI obligatorios**:
- Servicios Application → `Scoped`
- ViewModels WinUI → `Transient`
- UI Services (Session, Workspace) → `Singleton`
- `IPermissionCache` → `Singleton`

---

## 15. Performance

**No degradar**: grids, EF queries, navegación, observabilidad, runtime.

**Reutilizar**: `MemoryPermissionCache` (TTL 10 min), resolvers y servicios existentes.

**Patrón de carga lazy** en TabViews: flags booleanos + `LoadTab()` + `ILiveContextReporter`. No recargar datos al re-seleccionar un tab ya cargado.

---

## 16. MiniERP PYME Philosophy

Este ERP está orientado a **PYMES**.

**No convertirlo en**: SAP, Oracle, ERP financiero corporativo.

**Mantener**: simplicidad, rapidez, claridad operacional.

---

## 17. Finanzas

El módulo financiero es **Finanzas Operativas** (gastos, ingresos, cuentas por cobrar, cuentas por pagar, flujo operativo).

**No implementar**: contabilidad fiscal, SAT, IFRS, pólizas, contabilidad doble compleja.

---

## 18. Documentación Obligatoria

Toda implementación importante debe actualizar:
- `docs/ARCHITECTURE_STATUS.md`
- El documento del módulo correspondiente (o crear uno nuevo en `docs/`)

Documentar: cambios de arquitectura, decisiones importantes, riesgos, integración runtime, nuevos servicios, nuevas entidades, observaciones relevantes.

---

## 19. Build

Toda implementación debe terminar con **Build = 0 errores**.

**No dejar**: warnings graves, código muerto, TODOs críticos sin documentar.

**Verificar siempre**:
```powershell
dotnet build Ybridio.WinUI/Ybridio.WinUI.csproj -p:Platform=x64
```

---

## 20. Session Log

Al finalizar cada sesión de trabajo, actualizar `memory/session_log.md` con:
- Qué se hizo
- Estado actual (build OK, BD actualizada, etc.)
- Próximos pasos pendientes

---

## 21. Validación Final Obligatoria

Antes de cerrar cualquier implementación, Claude debe validar:

- ✔ Arquitectura intacta (4 capas respetadas)
- ✔ Separación de capas respetada
- ✔ Runtime Security intacto (no se rehízo Security Foundation)
- ✔ Runtime Observability intacta (no se rehízo el panel)
- ✔ WorkspaceService intacto
- ✔ Navegación principal intacta (Shell)
- ✔ UI consistente (estándar Outlook 2026)
- ✔ Build = 0 errores
- ✔ Documentación actualizada
- ✔ Fórmulas y cálculos críticos documentados (§25)
- ✔ XML docs en métodos públicos de Application e Infrastructure (§26)
- ✔ Sin números mágicos ni lógica implícita (§27)
- ✔ Decisiones arquitectónicas relevantes registradas en `DECISIONS.md` (§28)

---

## 22. Restricción Final

Si se cumple cualquiera de las siguientes condiciones, **la implementación es incorrecta**:

- Se rompe la arquitectura de capas
- Se rehacen componentes existentes innecesariamente
- Se mueve lógica incorrectamente entre capas
- Se rompe la observabilidad runtime
- Se hardcodean permisos (`if (rol == "Admin")`)
- Se degrada el performance de grids o navegación
- Se rompe la consistencia visual del ERP
- Build termina con errores
- Se introducen fórmulas o cálculos sin documentar (§25)
- Se usan números mágicos o lógica implícita no explicada (§27)
- Métodos críticos de Application/Infrastructure quedan sin XML doc (§26)

---

## 23. Filosofía General del Proyecto

El objetivo es construir un **miniERP PYME moderno, sólido, rápido y mantenible**.

Debe sentirse: enterprise · profesional · desacoplado · observable · seguro · operativo.

**Sin convertirse en** un ERP corporativo gigantesco e inmantenible.

---

---

# Documentation & Business Logic Rules

> Las secciones 24–30 definen los estándares obligatorios de documentación técnica, operacional y de lógica de negocio.  
> Una implementación sin documentación adecuada de su lógica NO está completa.

---

## 24. Documentación de Lógica Operacional

Toda lógica operacional importante **debe documentarse** en el código que la implementa.

Se considera lógica operacional importante:

- Cálculos y derivaciones de valores (saldos, totales, acumulados)
- Reglas de vencimiento y fechas límite
- Condiciones de autorización y flujos de aprobación
- Filtros runtime aplicados (scopes, empresa, sucursal, almacén)
- Reglas de negocio financieras (pagos parciales, liquidación, descuentos)
- Reglas de inventario (stock mínimo, costeo, existencia disponible)
- Movimientos acumulativos (kardex, saldo de caja, balance financiero)
- Validaciones no obvias (unicidad, integridad referencial de negocio)

**La regla de oro**: si un desarrollador nuevo leyera el método y no entendiera por qué se hace lo que se hace, falta documentación.

---

## 25. Estándares de Fórmulas

Toda fórmula de negocio debe documentar explícitamente:

1. **Qué calcula** — descripción funcional del resultado
2. **Fórmula utilizada** — expresión matemática o lógica
3. **Motivo funcional** — por qué el negocio lo calcula así
4. **Motivo arquitectónico** — por qué se tomó esta decisión de diseño
5. **Runtime vs persistido** — si se calcula en memoria o se almacena en BD
6. **Razón de esa decisión** — por qué no se persistió (o por qué sí)

### Fórmulas del dominio actual — referencia obligatoria

| Fórmula | Expresión | Persiste | Razón |
|---|---|---|---|
| `SaldoPendiente` | `MontoOriginal - MontoPagado` | ❌ Runtime | Calculado evita inconsistencias entre pagos parciales |
| `EsVencida` | `FechaVencimiento < hoy AND SaldoPendiente > 0` | ❌ Runtime | Depende de la fecha actual; persistir requeriría job diario |
| `SaldoAcumulado` | `SaldoAnterior + (Cantidad × Signo)` | ✅ BD | Kardex requiere trazabilidad histórica del saldo en cada movimiento |
| `ExistenciaDisponible` | `SUM(entradas) - SUM(salidas)` por producto+almacén | ✅ BD (`Existencia.Cantidad`) | Consultado frecuentemente en POS; recalcular desde kardex sería costoso |
| `SaldoCaja` | `MontoApertura + ingresos - egresos` | ✅ BD (`Caja.Saldo`) | Actualizado en cada MovimientoCaja; auditable por apertura |
| `CostoPromedio` | `(CostoActual × CantidadActual + CostoNuevo × CantidadNueva) / (CantidadActual + CantidadNueva)` | Pendiente | Implementar cuando se active costeo promedio en Inventario |

### Formato de documentación XML para fórmulas

```csharp
/// <summary>
/// Calcula el saldo pendiente de cobro.
/// </summary>
/// <remarks>
/// Fórmula: SaldoPendiente = MontoOriginal - MontoPagado
/// <para>
/// Calculado en runtime (no persistido) para garantizar consistencia
/// inmediata tras cada pago parcial sin riesgo de desincronización.
/// Si se persistiera, cada llamada a <see cref="RegistrarPagoAsync"/>
/// debería actualizar el campo — innecesario con EF tracking.
/// </para>
/// </remarks>
public decimal SaldoPendiente => MontoOriginal - MontoPagado;
```

---

## 26. XML Documentation — Métodos Críticos

Todo método `public` o `internal` en **Domain**, **Application** e **Infrastructure** requiere XML doc.

La obligación es especialmente estricta para:

- Métodos de Application Services (toda la interfaz + implementación)
- Cálculos financieros (fórmulas, razones de diseño)
- Cálculos de inventario (reglas de stock, costeo)
- Métodos con enforcement de seguridad (pre-conditions de permiso/scope)
- Métodos runtime críticos (autorizaciones, resolución de scopes)
- Métodos con side effects no obvios (modifica caché, lanza eventos)

### Qué documentar por tipo de método

| Tipo | `<summary>` mínimo | `<remarks>` recomendado |
|---|---|---|
| Listar/Consultar | Qué filtra, qué permisos valida | Filtros globales activos, scope aplicado |
| Crear/Actualizar | Validaciones que ejecuta, qué retorna | Reglas de unicidad, side effects |
| Fórmula/Cálculo | Fórmula explícita | Runtime vs persistido, razón arquitectónica |
| Autorización | Permiso requerido | Comportamiento si deniega, nivel de evaluación |
| Side effects | Qué estado modifica | Caché invalidado, eventos disparados |

### Reglas de omisión permitidas

- `<returns>` puede omitirse en `void` / `Task` sin valor significativo.
- `<param name="ct">` (CancellationToken) no se documenta — es evidente.
- Métodos privados solo se documentan cuando la lógica es no obvia o implementa un algoritmo.

### Anti-patrón — documentación vacía o redundante

```csharp
// ❌ NO: documenta lo obvio, no agrega valor
/// <summary>Obtiene el producto por ID.</summary>
Task<ProductoDto> ObtenerPorIdAsync(int productoId);

// ✅ SÍ: agrega contexto real
/// <summary>
/// Obtiene un producto por su ID incluyendo categorías, impuesto y unidad de medida.
/// Valida permiso <c>producto.ver</c> antes de retornar datos.
/// </summary>
/// <returns>
/// <see cref="ServiceResult{T}"/> con el <see cref="ProductoDto"/> completo,
/// o <see cref="ErrorCode.NotFound"/> si no existe,
/// o <see cref="ErrorCode.Unauthorized"/> si el usuario no tiene el permiso requerido.
/// </returns>
Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(int productoId, CancellationToken ct = default);
```

---

## 27. Sin Lógica Mágica

**Está prohibido** introducir lógica implícita, números mágicos o reglas de negocio ocultas sin documentación explícita.

### Prohibiciones concretas

```csharp
// ❌ Número mágico — ¿por qué 50? ¿qué representa?
.Take(50)

// ✅ Constante nombrada con razón
private const int MaxResultadosBusqueda = 50; // límite UX: más de 50 resultados no son útiles en búsqueda rápida
.Take(MaxResultadosBusqueda)

// ❌ Condición implícita — ¿qué significa SucursalId == 0?
if (Session.SucursalId != 0)

// ✅ Con contexto documentado
// SucursalId == 0 indica que el usuario no tiene sucursal activa asignada (recién autenticado o empresa sin sucursales)
if (Session.SucursalId != 0)

// ❌ Acumulado sin explicar
existencia.Cantidad -= cantidad;

// ✅ Con comentario de regla de negocio
// Descuenta stock: toda salida reduce la existencia en el almacén origen.
// El movimiento queda registrado en MovimientoInventario para trazabilidad (kardex).
existencia.Cantidad -= cantidad;
```

### Casos que SIEMPRE requieren comentario

- Cualquier valor numérico literal que no sea 0 o 1
- Condiciones de `if` con múltiples operadores combinados
- Comparaciones contra fechas calculadas (ej: `DateTime.Today.AddDays(-30)`)
- Reglas de estado implícitas (`Borrado = false`, `Activo = true` en creación)
- Comportamientos específicos de un provider/framework externo

---

## 28. Documentación de Decisiones Arquitectónicas

Toda decisión arquitectónica relevante debe registrarse en `docs/DECISIONS.md` como un nuevo ADR (Architecture Decision Record).

### Cuándo crear un nuevo ADR

- Se elige un enfoque sobre otro con trade-offs significativos
- Se decide NO implementar algo que podría esperarse (ej: sin migraciones EF, sin contabilidad doble)
- Se introduce un patrón nuevo al proyecto (primera vez que se usa un mecanismo)
- Se cambia una decisión anterior

### Cuándo documentar inline (comentario o XML doc)

Cuando la decisión afecta un método o tipo específico pero no es relevante a nivel global:

```csharp
// Usamos Join() explícito en lugar de navegación Include() porque EF Core
// no puede traducir el método MapToDto() client-side si va dentro de Select().
// Ver patrón establecido en ProductoService.ListarPorEmpresaAsync().
var lista = await query.Include(...).ToListAsync(ct);
return lista.Select(MapToDto).ToList();
```

### Formato mínimo de un ADR en DECISIONS.md

```
## ADR-NNN — Título breve

**Decisión**: qué se decidió hacer.
**Alternativas consideradas**: qué otras opciones existían.
**Razón**: por qué se tomó esta decisión (constraint, performance, simplicidad, negocio).
**Impacto**: qué archivos/patrones se ven afectados.
```

---

## 29. Documentación de Módulos

Todo módulo nuevo o significativamente extendido debe tener su propio documento en `docs/`.

### Módulos con documentación existente

| Módulo | Documento |
|---|---|
| Security Foundation | `docs/SECURITY_FOUNDATION.md` |
| Runtime Enforcement | `docs/RUNTIME_SECURITY_ENFORCEMENT.md` |
| Finanzas Operativas | `docs/FINANZAS_OPERATIVAS.md` |
| Estado de Arquitectura | `docs/ARCHITECTURE_STATUS.md` |
| Decisiones | `docs/DECISIONS.md` |
| Roadmap | `docs/ROADMAP.md` |
| Problemas conocidos | `docs/KNOWN_ISSUES.md` |

### Qué debe contener el documento de un módulo

1. **Objetivo** — qué problema resuelve, para qué tipo de usuario
2. **Qué incluye / qué NO incluye** — alcance intencional del módulo
3. **Estructura de datos** — tablas, entidades, campos clave, relaciones
4. **Fórmulas y cálculos** — todas las fórmulas de negocio del módulo (ver §25)
5. **Permisos** — claves de `PermisosClave.*` utilizadas
6. **Servicios** — interfaces, métodos expuestos, permisos validados
7. **Observabilidad** — cómo se integra con el Runtime Diagnostic Panel
8. **Limitaciones intencionales** — qué no hace y por qué
9. **Roadmap** — próximas mejoras planificadas

### Actualización de ARCHITECTURE_STATUS.md

Cada vez que se implementa o modifica un módulo:
- Actualizar la tabla de módulos con el estado real
- Actualizar el esquema de BD si se agregaron tablas
- Actualizar los servicios listados si se agregaron servicios

---

## 30. Documentación de Métodos con Side Effects o Dependencias Críticas

Los métodos que modifican estado compartido, disparan eventos, invalidan caché o tienen pre-conditions no obvias deben documentar:

### Side effects

```csharp
/// <summary>
/// Registra un pago parcial sobre la cuenta por cobrar indicada.
/// </summary>
/// <remarks>
/// Side effects:
/// - Incrementa <see cref="CuentaPorCobrar.MontoPagado"/> en la BD.
/// - Si MontoPagado alcanza MontoOriginal, el SaldoPendiente queda en 0
///   (la cuenta queda liquidada, pero NO se marca con un flag separado — el saldo
///   es la fuente de verdad).
/// - No invalida caché de permisos (no afecta seguridad).
/// </remarks>
```

### Dependencias de otros servicios o estado

```csharp
/// <summary>
/// Descuenta inventario y registra el movimiento de kardex en una sola transacción.
/// </summary>
/// <remarks>
/// Dependencias:
/// - Requiere que <see cref="Existencia"/> exista para el producto+almacén indicados.
/// - El <see cref="MovimientoInventario"/> se crea con SaldoAcumulado calculado
///   a partir del saldo previo en la misma operación (no recalculado a posteriori).
/// - Usa concurrencia optimista via RowVersion; lanzará <see cref="DbUpdateConcurrencyException"/>
///   si dos operaciones concurrentes intentan modificar la misma existencia.
/// </remarks>
```

### Filtros y scopes aplicados

```csharp
/// <summary>
/// Lista existencias con enforcement de autorización y scope de almacén.
/// </summary>
/// <remarks>
/// Filtros aplicados (en orden):
/// 1. Filtro global ErpDbContext: EmpresaId == session.EmpresaId (automático)
/// 2. Filtro global ErpDbContext: !Borrado (automático)
/// 3. Permiso runtime: existencia.ver via IErpAuthorizationService
/// 4. Scope de almacén: si el usuario tiene almacenes restringidos,
///    solo retorna existencias de esos almacenes específicos.
///    Si la lista está vacía (SuperAdmin o sin restricción), retorna todos.
/// </remarks>
```

---

## 15. Workspace Operational UX Stabilization

El Workspace (`IWorkspaceService` / `WorkspaceService`) es el sistema de pestañas persistentes del ERP.  
Conserva el estado de cada `Page` durante la sesión: filtros, grids, selección, scroll, contexto operacional.

### Objetivo

Evitar caos operacional en tabs/documentos:

- ❌ Tabs duplicados (e.g., Venta #91 abierta 3 veces)
- ❌ Tabs desordenados
- ❌ Foco inconsistente
- ❌ Navegación workflow confusa

✅ Garantizar:

- Single-instance: un solo tab por documento operacional
- Tab reuse: activar tab existente antes de crear uno nuevo
- Tab activation: foco automático al abrir/navegar
- Context preservation: preservar estado runtime (filtros, selección, etc.)

---

### Single-Document-Instance Policy

**Regla obligatoria**: un solo tab por documento operacional.

**Documentos operacionales** (single-instance):

- Venta
- Pedido
- Orden de Trabajo (OT)
- Cliente
- Producto
- Cotización

Si el usuario intenta abrir un documento ya abierto (e.g., "Venta #91"), el Workspace **activa el tab existente** en lugar de crear un duplicado.

**Módulos operacionales** (single-instance):

- Inventario
- Dashboard
- Administración

**Documentos nuevos** (no single-instance hasta guardar):

- Nueva Venta
- Nuevo Pedido
- Nueva OT

Estos usan keys no deduplicadas (e.g., `ot-nueva-{guid}`) hasta que se guardan y adquieren ID definitivo.

---

### Key Conventions

Formato estándar para claves de tabs:

| Tipo                       | Key Format                     | Ejemplo                        |
|----------------------------|--------------------------------|--------------------------------|
| Documento guardado         | `{tipo}-{id}`                  | `venta-91`, `pedido-55`, `ot-12` |
| Documento nuevo            | `{tipo}-nueva-{guid}`          | `venta-nueva-abc123`           |
| Módulo operacional         | `{modulo}`                     | `inventario`, `dashboard`      |

**Importante**: el `key` determina la deduplicación. Dos tabs con la misma `key` **no pueden coexistir**.

---

### Title Conventions

Formato estándar para títulos runtime de tabs:

| Tipo                       | Title Format                   | Ejemplo                        |
|----------------------------|--------------------------------|--------------------------------|
| Documento guardado         | `{Tipo} #{id}`                 | `Venta #91`, `OT #12`          |
| Documento nuevo            | `Nuevo/Nueva {Tipo}`           | `Nueva Venta`, `Nuevo Pedido`  |
| Módulo operacional         | Nombre completo                | `Inventario`, `Dashboard`      |

---

### Workflow de Apertura de Documentos

**Método recomendado**: `IWorkspaceService.OpenOrActivateDocumentTabAsync<TData>`

Este helper centraliza el patrón de apertura/reuso:

1. Si el documento ya existe (`Exists(key)`): **activa el tab existente** (`ActivateTab(key)`)
2. Si no existe: carga los datos (`await dataLoader()`), crea el tab (`OpenTab(...)`) y **activa automáticamente**

**Ejemplo** (antes y después):

**❌ ANTES** (manual, repetitivo, propenso a errores):

```csharp
private async void AbrirVentaEnWorkspace(long ventaId)
{
    var key = $"venta-{ventaId}";
    if (_workspace.Exists(key)) { _workspace.ActivateTab(key); return; }

    var result = await _ventaService.ObtenerConDetallesAsync(ventaId);
    if (!result.Success) { ViewModel.ErrorMessage = result.Error; return; }

    _workspace.OpenTab(
        key:         key,
        title:       $"Venta #{ventaId}",
        icon:        "",
        pageFactory: () => new VentaDocumentoPage(result.Value),
        isClosable:  true);
}
```

**✅ DESPUÉS** (centralizado, consistente, single-instance automático):

```csharp
private async void AbrirVentaEnWorkspace(long ventaId)
{
    await _workspace.OpenOrActivateDocumentTabAsync(
        key:         $"venta-{ventaId}",
        title:       $"Venta #{ventaId}",
        icon:        "",
        dataLoader:  () => _ventaService.ObtenerConDetallesAsync(ventaId)
                            .ContinueWith(t => t.Result.Success ? t.Result.Value : null),
        pageFactory: dto => new VentaDocumentoPage(dto!),
        onError:     err => ViewModel.ErrorMessage = err,
        isClosable:  true);
}
```

---

### Tab Activation Rules

- Cuando un workflow abre un documento: **activar automáticamente el tab** (nuevo o existente)
- Cuando el usuario cambia de tab: **preservar contexto runtime** (filtros, selección, scroll)
- Cuando se cierra el tab activo: **activar el tab vecino** (mismo índice o último disponible)

---

### Context Preservation

`WorkspaceTabItem.Content` mantiene la instancia de `Page` viva durante todo el ciclo de vida del tab.

Esto preserva:

- Estado del ViewModel (filtros, búsquedas, selecciones)
- Scroll position en grids
- Datos cargados (no reload innecesario)
- Dirty state (`IsDirty`)

**NO** destruir ni recrear la `Page` al cambiar de tab.

---

### Anti-Patterns

**❌ NO hacer**:

- Crear tabs duplicados del mismo documento
- Recargar datos innecesariamente al cambiar tabs
- Dejar tabs abiertos sin foco después de workflows
- Usar keys ambiguos (e.g., `documento-1` sin tipo)
- Perder contexto runtime al navegar
- Implementar lógica de negocio en `WorkspaceService` (solo navegación/coordinación)

---

### Runtime Diagnostic Integration

El Workspace debe integrarse con el Runtime Diagnostic Panel:

- Tab activo (`ActiveTab`)
- Tabs reutilizados (evitados duplicados)
- Navegación workflow
- Contexto operacional activo
- Tiempo de vida de tabs (`WorkspaceTabItem.CreatedAt`)

Esto facilita debugging operacional y observabilidad runtime.

---

### Performance

Mantener:

- Navegación rápida entre tabs
- Activación inmediata sin latencia perceptible
- Bajo overhead runtime (no re-render innecesario)

**NO** agregar:

- Recalcular ViewModels al cambiar tabs
- Reload masivo de datos al navegar
- Animaciones/transiciones pesadas

---

### Workspace vs WindowManager

**Workspace** (`IWorkspaceService`):

- Tabs persistentes durante la sesión
- Módulos principales (Inventario, Ventas, Dashboard)
- Documentos operacionales (Venta, Pedido, OT)
- Estado preservado (grids, filtros, contexto)

**WindowManager** (`IWindowManager`):

- Ventanas auxiliares (dialogs, popups)
- Ventanas temporales (selección, búsqueda)
- Ventanas que se destruyen al cerrarse

**NO** mezclar responsabilidades. El Workspace NO es para dialogs; `IWindowManager` NO es para documentos persistentes.

---

## 16. Workspace Visual Hierarchy

El ERP usa una arquitectura visual de **dos capas de tabs** para evitar confusión operacional y ensimamiento visual:

1. **Workspace Layer** (documentos persistentes): dominante, permanente, ERP-like
2. **Module Layer** (navegación interna): secundario, contextual, navegacional

### Objetivo

Evitar caos visual donde Workspace Tabs y Module Tabs parezcan un solo control ensimado.

✅ El usuario debe diferenciar inmediatamente:
- **Documentos abiertos** (Venta #91, Pedido #55, OT #12) — Workspace Layer
- **Navegación de módulo** (Cotizaciones, Pedidos, Ventas) — Module Layer

---

### Workspace Layer (documentos persistentes)

**Estilo**: `WorkspaceTabItemStyle` (definido en `App.xaml`)

**Características visuales**:
- **Height**: MinHeight=48 (vs 40 module)
- **Padding**: 18,12,6,12 (vs 16,8,4,8 module)
- **Background**: `SubtleFillColorSecondaryBrush` (normal), `LayerFillColorDefaultBrush` (selected)
- **SelectionBar**: Height=4, Margin=8,0 (más prominente que module)
- **CloseButton**: 22x22, ToolTip "Cerrar documento"
- **Typography**: SemiBold cuando selected
- **Separación vertical**: Margin 0,12,0,0 desde contenido de módulo

**Ubicación**: `ShellPage.xaml` WorkspaceTabView (línea ~298)

**Aplicación**:
```xaml
<TabView x:Name="WorkspaceTabView"
         Margin="0,12,0,0"
         Background="{ThemeResource LayerOnMicaBaseAltFillColorDefaultBrush}">
    <TabView.Resources>
        <Style TargetType="TabViewItem" BasedOn="{StaticResource WorkspaceTabItemStyle}"/>
    </TabView.Resources>
</TabView>
```

**Debe sentirse**:
- Dominante
- Persistente
- Principal
- Tipo IDE/ERP
- Documentos abiertos

---

### Module Layer (navegación interna)

**Estilo**: `OutlookTabItemStyle` (definido en `App.xaml`)

**Características visuales**:
- **Height**: MinHeight=40 (compacto)
- **Padding**: 16,8,4,8 (moderado)
- **Background**: Transparent (normal), sin background hover/selected
- **SelectionBar**: Height=3, Margin=6,0 (sutil)
- **CloseButton**: 20x20, ToolTip "Cerrar" (normalmente IsClosable=False)
- **Typography**: SemiBold cuando selected
- **Sin separación vertical adicional**: dentro del flujo de página

**Ubicación**: Páginas de módulos (`VentasPage.xaml`, `FinanzasPage.xaml`, `InventarioPage.xaml`, `ConfiguracionPage.xaml`)

**Aplicación**:
```xaml
<Page.Resources>
    <Style TargetType="TabViewItem" BasedOn="{StaticResource OutlookTabItemStyle}"/>
</Page.Resources>

<TabView x:Name="VentasTabs"
         TabWidthMode="SizeToContent"
         IsAddTabButtonVisible="False"
         Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">
    <TabViewItem Header="Cotizaciones" IsClosable="False">
        <Frame x:Name="FrameCotizaciones"/>
    </TabViewItem>
    <!-- ... -->
</TabView>
```

**Debe sentirse**:
- Secundario
- Navegacional
- Contextual
- Parte del módulo activo
- No compite con Workspace

---

### Comparación visual

| Característica | Workspace Layer | Module Layer |
|---|---|---|
| **Estilo** | `WorkspaceTabItemStyle` | `OutlookTabItemStyle` |
| **Rol** | Documentos persistentes | Navegación módulo |
| **MinHeight** | 48px | 40px |
| **Padding** | 18,12,6,12 | 16,8,4,8 |
| **Background (normal)** | SubtleFillColorSecondaryBrush | Transparent |
| **Background (selected)** | LayerFillColorDefaultBrush | Transparent |
| **SelectionBar** | Height=4, Margin=8,0 | Height=3, Margin=6,0 |
| **CloseButton** | 22x22 | 20x20 |
| **Separación vertical** | Margin 0,12,0,0 | — |
| **Jerarquía visual** | Dominante, principal | Secundario, contextual |

---

### Spacing & Layout Rules

**Workspace TabView separación**:
- **Margin superior**: 12px desde contenido de módulo
- **Background**: `LayerOnMicaBaseAltFillColorDefaultBrush` para diferenciación sutil del módulo
- **Z-index**: Capa 2 (se superpone al ModuleFrame cuando visible)

**Module TabView container hierarchy**:
- **OBLIGATORIO**: Module TabView SIEMPRE dentro de Border wrapper con fondo sólido sutil
- **Padding estándar**: `Padding="16,12,16,16"` (left, top, right, bottom) — 12px superior para separación física
- **Background recomendado**: `{ThemeResource LayerFillColorDefaultBrush}` en el Border
- **TabView Background**: `"Transparent"` (el fondo lo provee el Border container)
- **Propósito**: crear boundary físico visible que separa Module Layer del Workspace Layer

**Patrón de implementación**:
```xaml
<!-- Module page (VentasPage, FinanzasPage, InventarioPage, ConfiguracionPage) -->
<Border Padding="16,12,16,16"
        Background="{ThemeResource LayerFillColorDefaultBrush}">
    <TabView x:Name="ModuleTabs"
             Background="Transparent"
             TabWidthMode="SizeToContent"
             IsAddTabButtonVisible="False"
             SelectionChanged="...">
        <!-- TabViewItems -->
    </TabView>
</Border>
```

---

### Visual Container Hierarchy

**Objetivo**: eliminar efecto "tabs transparentes/ensimados" mediante separación física real entre capas visuales.

**Regla obligatoria**: Todo Module TabView DEBE vivir dentro de un **container visual explícito** (Border/Grid) con:
1. **Background sólido sutil** — NO `Transparent`, usar `LayerFillColorDefaultBrush` o `CardBackgroundFillColorDefaultBrush`
2. **Padding superior real** (12px mínimo) — separación física desde el borde de la página
3. **Border boundary** — contenedor visible que define el Module Layer como superficie independiente

**NO depender únicamente de Margin** — insuficiente para separación visual real.

**Resultado esperado**:
- Module Layer se siente **contenido dentro del documento activo**
- Workspace Layer se siente **externo/superior**
- Separación física visible — **NO** tabs flotando sobre tabs
- Boundary claro — container background sólido vs Workspace background

---

### TabView Content Host Separation (Workspace Layer)

**Problema**: WinUI TabView coloca el header region (TabViewItem + SelectionIndicator) y el content host **sin separación vertical estructural**, causando overlap visual donde el underline/selection bar invade el contenido.

**Regla obligatoria**: El **WorkspaceTabView** en ShellPage.xaml DEBE tener **Padding top estructural** suficiente para separar físicamente el content host del header region.

**Cálculo de Padding requerido**:
- `WorkspaceTabItemStyle` MinHeight: **48px**
- SelectionBar height: **4px**
- Espaciado visual adicional: **8px**
- **Total Padding top: 60px**

**Implementación correcta**:
```xaml
<TabView x:Name="WorkspaceTabView"
         Margin="0,12,0,0"
         Padding="0,60,0,0"
         Background="{ThemeResource LayerOnMicaBaseAltFillColorDefaultBrush}">
    <TabView.Resources>
        <Style TargetType="TabViewItem" BasedOn="{StaticResource WorkspaceTabItemStyle}"/>
    </TabView.Resources>
</TabView>
```

**Diferencia con Module Layer**:
- **Module TabViews**: usan **Border wrapper externo** con Padding para crear container boundary
- **WorkspaceTabView**: usa **Padding interno directo** para separar header de content host (no necesita Border porque ya vive en layer superior del Shell)

**NO usar hacks**:
- ❌ Margins gigantes arbitrarios
- ❌ TranslateTransform / RenderTransform offsets
- ❌ Z-index tricks
- ❌ Opacity manipulations
- ❌ Negative margins

**✅ HACER**:
- Padding estructural calculado (header height + selection bar + spacing visual)
- Declarativo/limpio en XAML
- Escalable a diferentes DPI/resoluciones
- Sin overhead runtime

---

### Anti-Patterns

**❌ NO hacer**:
- Usar el mismo estilo para ambas capas (confusión visual)
- Tabs workspace sin separación vertical del módulo (ensimamiento)
- Module tabs con height/padding igual a workspace (jerarquía rota)
- Backgrounds agresivos o colores llamativos (mantener Outlook 2026 sutil)
- TabView dentro de TabView sin diferenciación clara
- Workspace tabs visualmente secundarios (pierde jerarquía)
- Module tabs visualmente dominantes (compite con workspace)
- **Module TabView sin container boundary físico** (tabs transparentes/ensimados)
- **Module TabView con Background="Transparent" directo en Page** (sin Border wrapper)
- **Module TabView sin padding superior** (pegado al borde de página, sin separación del Workspace)
- **Depender solo de Margin para separación visual** (insuficiente, necesita background boundary)
- **WorkspaceTabView sin Padding top estructural** (overlap header/content host)
- **Usar hacks visuales para separación de content host** (TranslateTransform, z-index, margins arbitrarios gigantes)

**✅ HACER**:
- Aplicar `WorkspaceTabItemStyle` solo al WorkspaceTabView en ShellPage
- Aplicar `OutlookTabItemStyle` a todos los module TabViews
- Mantener Margin 0,12,0,0 en WorkspaceTabView para separación del ModuleFrame
- **Padding="0,60,0,0" en WorkspaceTabView** para separación estructural header/content host
- Background sutil en workspace, transparent en module
- Diferenciación inmediata: workspace = documentos, module = navegación
- **Envolver Module TabView en Border con Padding="16,12,16,16" y Background="{ThemeResource LayerFillColorDefaultBrush}"**
- **Module TabView Background="Transparent"** (el fondo lo provee el Border)
- **Container boundary físico visible** para Module Layer

---

### Performance

Mantener:
- Render estable (no introducir layouts complejos innecesarios)
- Navegación fluida (tabs no agregan overhead visual)
- Bajo impacto: solo estilos XAML estáticos, sin bindings runtime pesados

**NO** introducir:
- Animaciones/transiciones pesadas en tab switching
- Re-render innecesario al cambiar estilos
- Layouts nested complejos que afecten scrolling

---

### UX Esperado

El usuario debe poder:
1. **Diferenciar inmediatamente** documentos abiertos (workspace) vs navegación de módulo
2. **Ver claramente** qué documento está activo (workspace tab selected)
3. **Navegar** entre tabs de módulo sin confundirlas con documentos workspace
4. **Trabajar multi-documento** sin caos visual ni tabs ensimados
5. **Operar el ERP durante horas** con experiencia limpia, estable, profesional

El Workspace debe sentirse:
- **Limpio** — no ensimado visualmente
- **Estable** — jerarquía clara y predecible
- **Profesional** — ERP-like, no browser tabs caótico
- **Operacional** — flujo de trabajo moderno y cómodo
- **Moderno** — Outlook 2026 style, subtle, elegant

---

## 12. Document Surface UX Pattern (§ADR-025)

### Objetivo

Reducir el caos de Workspace Tabs innecesarios para operaciones CRUD ligeras/contextuales, usando **Document Surfaces** embebidos dentro del módulo activo que reemplazan temporalmente el grid de listado.

### Principio

**Workspace Tabs** = workflows persistentes, multi-documento, complejos, importantes.  
**Document Surfaces** = operación rápida contextual (Nuevo/Editar/Abrir) que no requiere tab persistente.

---

### Reglas Oficiales UX

#### 1. Layout: Content Replacement

**USAR**:
- `ContentPresenter` o panel reemplazable dentro del módulo
- Un solo contenido visible a la vez: **grid de listado XOR Document Surface**
- Cuando el surface está activo, el grid se oculta completamente

**NO**:
- Split view permanente
- Grid de dos columnas (listado | surface)
- Layouts master-detail complejos

**Razón**:
- UX más limpia
- Menos ruido visual
- Mayor enfoque operacional
- Mejor para PYME

#### 2. Transiciones

**NO** implementar animaciones complejas.

**USAR**:
- Transición instantánea o muy sutil
- Cambio directo de visibilidad mediante binding

**Razón**:
- ERP operacional debe sentirse rápido
- WinUI animations excesivas degradan UX
- Evitar complejidad innecesaria
- Objetivo: **fluidez > efectos visuales**

#### 3. Comportamiento Guardar

**Después de Guardar**:
1. Refrescar automáticamente el grid de listado
2. Cerrar el Document Surface
3. Volver al listado

**Flujo típico PYME**:
```
crear → guardar → seguir trabajando en lista
```

**NO** dejar el surface abierto automáticamente para CRUDs ligeros.

**EXCEPCIÓN futura**: workflows largos/OT complejas (aún no migrados).

#### 4. Navegación "← Volver a Lista"

**Agregar botón claro**:
- Ubicación: primer botón en CommandBar del Document Surface
- Texto: `"Volver a Lista"` o `"← Volver"`
- Icon: `&#xE72B;` (Back)
- Acción: cerrar surface sin guardar, volver al grid

**Razón**:
- Permitir cancelar sin guardar
- Navegación explícita y clara
- Contexto visual de "dónde estoy"

#### 5. Migración Inicial (Piloto)

**Aplicar PRIMERO solamente a**:
- Cotizaciones ✅
- Clientes (pendiente)
- Productos (pendiente)

**NO migrar todavía**:
- Pedidos (workflow complejo)
- Ventas (puede generar otros documentos)
- OT (multi-paso, diseño/producción)

**Razón**:
- Piloto controlado
- Validar UX antes de expansión
- Observar aceptación operacional y estabilidad runtime

#### 6. Workflows Complejos

**Workflows complejos permanecen usando Workspace Tabs persistentes**.

**Ejemplos de workflows que NO deben usar Document Surfaces**:
- OT complejas (diseño → producción → QA)
- Multi-documento (Venta ↔ Pedido ↔ OT)
- Comparación/análisis (necesita múltiples documentos visibles)
- Workflows operacionales largos

**Document Surface es para**:
- CRUD rápido
- Edición ligera
- Mantenimiento contextual
- Operaciones que normalmente se completan en una sola sesión

---

### Arquitectura del Pattern

#### ViewModel del Módulo

```csharp
// CotizacionesViewModel (listado)
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
    await RefrescarAsync(); // Refrescar grid
}
```

#### Page del Módulo (XAML)

```xaml
<Grid Grid.Row="2">
    <!-- Listado (visible cuando IsDocumentSurfaceVisible = false) -->
    <Border Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay, 
                                  Converter={StaticResource InverseBoolToVisibilityConverter}}">
        <ListView ItemsSource="{x:Bind ViewModel.Items, Mode=OneWay}" ... />
    </Border>

    <!-- Document Surface (visible cuando IsDocumentSurfaceVisible = true) -->
    <ContentPresenter Content="{x:Bind ViewModel.DocumentSurfaceContent, Mode=OneWay}"
                      Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay}"/>
</Grid>
```

#### Page del Módulo (Code-Behind)

```csharp
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
    ViewModel.SuccessMessage = "Guardado correctamente.";
}

private async void OnVolverALista()
{
    await ViewModel.CerrarDocumentSurfaceAsync();
}
```

#### ViewModel del Documento

```csharp
// CotizacionDocumentoViewModel
public Action? DocumentSaved;

[RelayCommand]
public async Task GuardarAsync(CancellationToken ct = default)
{
    // ... lógica de guardado ...
    if (IsNuevo)
    {
        var r = await _service.CrearAsync(dto, _session.Usuario.Id, ct);
        if (!r.Success) { ErrorMessage = r.Error; return; }
        Initialize(r.Value);
        DocumentSaved?.Invoke(); // ← Notificar al módulo
    }
    else
    {
        var r = await _service.ActualizarAsync(_documento!.Id, dto, _session.Usuario.Id, ct);
        if (!r.Success) { ErrorMessage = r.Error; return; }
        DocumentSaved?.Invoke(); // ← Notificar al módulo
    }
}
```

#### Page del Documento (XAML)

```xaml
<CommandBar>
    <!-- Botón "← Volver a Lista" visible solo en Document Surface -->
    <AppBarButton x:Name="BtnVolverALista" Label="Volver a Lista" Click="BtnVolverALista_Click">
        <AppBarButton.Icon><FontIcon Glyph="&#xE72B;"/></AppBarButton.Icon>
    </AppBarButton>
    <AppBarSeparator/>
    <AppBarButton Label="Guardar" Command="{x:Bind ViewModel.GuardarCommand}">
        <AppBarButton.Icon><FontIcon Glyph="&#xE74E;"/></AppBarButton.Icon>
    </AppBarButton>
    ...
</CommandBar>
```

#### Page del Documento (Code-Behind)

```csharp
public Action? VolverALista { get; set; }

private void BtnVolverALista_Click(object sender, RoutedEventArgs e)
{
    VolverALista?.Invoke();
}
```

---

### Anti-Patterns

**❌ NO hacer**:
- Usar Document Surface para workflows complejos/multi-documento
- Dejar el surface abierto después de guardar (para CRUDs ligeros)
- Implementar animaciones complejas de transición
- Usar split view o layouts master-detail permanentes
- Abrir Workspace Tabs para operaciones CRUD simples (Nueva Cotización, Editar Cliente)
- Migrar todos los módulos de golpe sin validar el piloto

**✅ HACER**:
- Workspace Tabs para workflows persistentes/importantes
- Document Surfaces para CRUD rápido contextual
- Cerrar y refrescar después de guardar (flujo PYME)
- Transición instantánea/sutil
- Botón "← Volver a Lista" claro
- Validar piloto (Cotizaciones/Clientes/Productos) antes de expandir
- Preservar WorkspaceService intacto (no rehacer)

---

### Validación UX Obligatoria

Confirmar:
- ✅ Menos caos de tabs
- ✅ Navegación más natural
- ✅ Contexto de módulo preservado
- ✅ Operación más rápida
- ✅ Flujo PYME (crear → guardar → seguir trabajando)
- ✅ Runtime Observability funcional
- ✅ WorkspaceService intacto

---
