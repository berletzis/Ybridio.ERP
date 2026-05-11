# ADR-029 — Window Management Standards: Centralized Runtime Authority

**Fecha**: 2026-05-10  
**Estado**: ✅ IMPLEMENTADO  
**Impacto**: WinUI + Services + Runtime + Documentation  
**Relacionado**: ADR-028 Document Surface Window Detach Mode

---

## Contexto

Durante la implementación del **Window Detach Mode (ADR-028)** se creó inicialmente un servicio paralelo `IDetachedWindowManager`/`DetachedWindowManager` que duplicaba funcionalidad del **`WindowManager`** existente, violando el principio de **single source of truth** para window lifecycle management.

**Problema identificado**:
- Dos sistemas de window management paralelos: `WindowManager` (oficial, completo) + `DetachedWindowManager` (duplicado, bypass)
- Tracking duplicado de ventanas activas (`_windows` en WindowManager + `_activeWindows` en DetachedWindowManager)
- Lifecycle management inconsistente (ownership Win32, cleanup handlers, z-order, focus)
- Riesgo de leaks por cleanup disperso
- Policy enforcement fragmentado (límite máximo 2 ventanas detached vivía en manager secundario)
- Violación DRY: lógica `new Window()`, `AppWindow.GetFromWindowId(...)`, `Resize`, `Activate`, `Closed += ...` duplicada

**Anti-pattern institucionalizado**: Window management manual disperso en Pages/ViewModels/Services sin autoridad centralizada runtime.

---

## Decisión

**Formalizar `WindowManager` como single source of truth OBLIGATORIO para TODO window lifecycle management en el ERP.**

Toda ventana runtime (detached, dialog, detail, wizard, secondary) debe:
1. Crearse vía `WindowManager.OpenWindow<TWindow, TKey>(...)`
2. Registrarse automáticamente en tracking centralizado
3. Obedecer policy global de límites (ej: máximo 2 detached windows)
4. Cleanup automático via handler `window.Closed`
5. Ownership Win32 automático para z-order garantizado
6. Logging diagnóstico centralizado

**Prohibición explícita**:
- `new Window()` fuera de `WindowManager`
- `AppWindow` manual disperso
- Lifecycle handlers duplicados
- Tracking paralelo de ventanas
- Window ownership manual fuera del manager
- Services window management secundarios

---

## Implementación

### Extensión de `WindowManager` para Window Detach Mode

#### 1. Policy global detached windows

```csharp
// WindowManager.cs

private int _detachedWindowsCount; // ADR-028: Window Detach Mode tracking

private const int MaxDetachedWindows = 2;
private const string DetachedKeyPrefix = "detached:";
```

**Razón**: Límite arquitectónico máximo 2 ventanas detached (ADR-028) vive en `WindowManager` como policy runtime global, NO per-module, NO en ViewModels.

#### 2. Detección automática y enforcement

```csharp
public void OpenWindow<TWindow, TKey>(TKey key, Func<TWindow> factory, WindowOptions? options = null)
	where TWindow : Window
{
	// ...
	var internalKey = BuildKey<TWindow, TKey>(key);

	// ── Policy: Window Detach Mode Limit (ADR-028) ──────────────────────
	var isDetachedWindow = internalKey.StartsWith(DetachedKeyPrefix, StringComparison.Ordinal);
	if (isDetachedWindow && _detachedWindowsCount >= MaxDetachedWindows)
	{
		_logger.LogWarning("[WindowManager] Límite detached windows alcanzado ({Current}/{Max}): {Key}",
			_detachedWindowsCount, MaxDetachedWindows, internalKey);
		throw new DetachedWindowLimitException(MaxDetachedWindows, _detachedWindowsCount);
	}

	// Reutilizar instancia existente — nunca duplicar
	if (_windows.TryGetValue(internalKey, out var existing))
	{
		BringDescriptorToFront(existing);
		return;
	}

	// Crear via factory, establecer ownership, configurar tamaño/posición...
	var window = factory();
	// ...

	// Incrementar contador detached si aplica
	if (isDetachedWindow)
	{
		_detachedWindowsCount++;
	}

	// Limpiar al cerrar
	window.Closed += (_, _) =>
	{
		_windows.Remove(internalKey);

		if (isDetachedWindow)
		{
			_detachedWindowsCount = Math.Max(0, _detachedWindowsCount - 1);
		}

		_logger.LogDebug("[WindowManager] Ventana cerrada: {Key}", internalKey);
	};

	// Activar ventana...
}
```

**Convention over configuration**: Ventanas con key prefix `"detached:"` activan policy límite 2. Otras ventanas (`"dialog:"`, `"detail:"`, etc.) NO cuentan contra este límite.

#### 3. Excepción operacional

```csharp
// DetachedWindowLimitException.cs

public sealed class DetachedWindowLimitException : InvalidOperationException
{
	public int MaxAllowed { get; }
	public int CurrentCount { get; }

	public DetachedWindowLimitException(int maxAllowed, int currentCount)
		: base($"Límite alcanzado: máximo {maxAllowed} ventanas desacopladas simultáneas. Cierre alguna ventana antes de abrir otra. (Activas: {currentCount})")
	{
		MaxAllowed   = maxAllowed;
		CurrentCount = currentCount;
	}
}
```

**Razón**: Excepción tipada permite manejo claro en UI (`try/catch` + `ContentDialog`) sin parsing de mensajes genéricos.

---

### Window helper code-only

```csharp
// Ybridio.WinUI/Views/Detached/DetachedDocumentWindow.cs

public sealed class DetachedDocumentWindow : Window
{
	public DetachedDocumentWindow(Page documentPage, string title)
	{
		Title = title;

		var grid = new Grid
		{
			Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"]
		};

		grid.Children.Add(new ContentPresenter { Content = documentPage });
		Content = grid;

		ExtendsContentIntoTitleBar = false; // Title bar estándar
	}
}
```

**Razón**: Code-only evita complejidad XAML source generator; ventana contiene SOLO documento, NO shell ERP.

---

### Uso en CotizacionDocumentoPage

```csharp
// CotizacionDocumentoPage.xaml.cs

private readonly IWindowManager _windowManager; // ADR-029: Centralized Window Management

private void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
{
	var titulo = _cotizacionOriginal is not null
		? $"Cotización - {_cotizacionOriginal.NombreCliente}"
		: "Nueva Cotización";

	var cotizacionId = _cotizacionOriginal?.Id.ToString() ?? Guid.NewGuid().ToString();
	var detachedKey  = $"detached:cotizacion:{cotizacionId}"; // Key prefix activa policy

	try
	{
		_windowManager.OpenWindow<DetachedDocumentWindow, string>(
			key: detachedKey,
			factory: () =>
			{
				var nuevaPagina = new CotizacionDocumentoPage(_cotizacionOriginal);
				return new DetachedDocumentWindow(nuevaPagina, titulo);
			},
			options: new WindowOptions
			{
				Width  = 1200,
				Height = 800,
				PositionStrategy = WindowPositionStrategy.CenterScreen
			});
	}
	catch (DetachedWindowLimitException ex)
	{
		// Mostrar mensaje operacional al usuario
		_ = MostrarMensajeLimiteVentanasAsync(ex);
	}
}
```

**Razón**: Una sola línea de lógica window management; toda complejidad lifecycle/ownership/tracking/cleanup vive en `WindowManager`.

---

### Eliminación de `IDetachedWindowManager`

**Archivos eliminados**:
- `Ybridio.WinUI/Services/Workspace/IDetachedWindowManager.cs`
- `Ybridio.WinUI/Services/Workspace/DetachedWindowManager.cs`

**DI registration removida**:
```csharp
// App.xaml.cs (ANTES)
services.AddSingleton<IDetachedWindowManager, DetachedWindowManager>(); // ADR-028: Window Detach Mode

// App.xaml.cs (DESPUÉS)
services.AddSingleton<IWindowManager, WindowManager>(); // ADR-029: Centralized Window Management (incluye Window Detach Mode)
```

**Razón**: Un solo servicio window management, una sola policy runtime, una sola fuente de verdad lifecycle.

---

## Beneficios

### Centralización
- **Single source of truth**: `WindowManager` es la autoridad runtime única para ventanas
- **Policy enforcement consistente**: límites globales (ej: máximo 2 detached) aplicados uniformemente
- **Tracking consolidado**: `_windows` dictionary único + `_detachedWindowsCount` contador global
- **Lifecycle centralizado**: creación, activación, ownership, cleanup en un solo lugar

### Simplificación
- **Pages/ViewModels limpios**: NO contienen lógica window management (solo `_windowManager.OpenWindow(...)`)
- **DRY eliminado**: NO duplicación de `new Window()`, `AppWindow.GetFromWindowId(...)`, `Resize`, `Activate`, `Closed += ...`
- **Convention over configuration**: key prefix `"detached:"` activa policy, NO flags/enums/config adicional

### Observabilidad
- **Logging centralizado**: `_logger.LogDebug("[WindowManager] ...")` captura TODO lifecycle runtime
- **Diagnóstico runtime**: tracking `_detachedWindowsCount` permite futura integración con Runtime Diagnostic Panel
- **Error handling claro**: `DetachedWindowLimitException` tipada para manejo UI limpio

### Mantenibilidad
- **Extend point claro**: agregar nuevas policies (ej: máximo 3 dialogs, solo 1 wizard) se hace en `WindowManager` únicamente
- **Testing simplificado**: mock `IWindowManager` en tests, NO múltiples managers
- **Refactors seguros**: cambios en lifecycle afectan un solo archivo (`WindowManager.cs`), NO disperso en codebase

---

## Anti-patterns explícitos (PROHIBIDO)

### ❌ Window management manual disperso

```csharp
// ❌ PROHIBIDO: new Window() fuera de WindowManager
var window = new Window { Title = "Detalle Producto" };
var hwnd = WindowNative.GetWindowHandle(window);
var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
appWindow.Resize(new SizeInt32(900, 700));
window.Activate();

// ✅ CORRECTO: usar WindowManager
_windowManager.OpenWindow<ProductoDetailWindow, int>(
	productoId,
	() => new ProductoDetailWindow(producto),
	new WindowOptions { Width = 900, Height = 700 });
```

### ❌ Services window management paralelos

```csharp
// ❌ PROHIBIDO: crear IDetachedWindowManager, IDialogManager, IWizardManager secundarios
services.AddSingleton<IDetachedWindowManager, DetachedWindowManager>();
services.AddSingleton<IDialogManager, DialogManager>();

// ✅ CORRECTO: un solo WindowManager con policies internas
services.AddSingleton<IWindowManager, WindowManager>();
```

### ❌ Tracking paralelo ventanas

```csharp
// ❌ PROHIBIDO: counters locales en ViewModels/Pages
private int _ventanasAbiertasPorMi = 0;

// ✅ CORRECTO: WindowManager rastrea TODO centralmente
// NO tracking adicional en UI layer
```

### ❌ Lifecycle handlers duplicados

```csharp
// ❌ PROHIBIDO: window.Closed += ... manual fuera de WindowManager
window.Closed += (s, e) => { /* cleanup manual */ };

// ✅ CORRECTO: WindowManager maneja Closed automáticamente
// Factory solo crea ventana; WindowManager maneja lifecycle
```

### ❌ Policy enforcement fragmentado

```csharp
// ❌ PROHIBIDO: validaciones límite en ViewModels
if (_ventanasDetachedAbiertas >= 2) { /* error */ }

// ✅ CORRECTO: WindowManager valida policy y lanza DetachedWindowLimitException
// UI solo captura excepción y muestra mensaje
```

---

## Concurrency considerations

### Multi-window runtime
- `WindowManager` es singleton thread-safe: `DispatcherQueue.HasThreadAccess` + `TryEnqueue` garantiza UI thread
- Ventanas detached múltiples (hasta 2) tienen **state independiente**: cada instancia tiene su propia página/ViewModel
- NO compartir state mutable entre ventanas; snapshot DTO original garantiza independencia

### Security runtime
- Cada ventana evalúa permisos **independientemente** (NO cachear permisos globalmente)
- `PermisoService` single-flight guard (ADR-026) serializa evaluaciones concurrentes
- Lifecycle events (`Window.Closed`, `Window.Activated`) NO provocan concurrency DbContext si state es independiente

### Cleanup
- `window.Closed += ...` handler automático en `WindowManager` decrementa contadores y limpia tracking
- Dictionary `_windows` se limpia automáticamente; NO memory leaks si event handler se registra correctamente

---

## Runtime Observability (integración futura)

**Pendiente**: Extender `RuntimeDiagnosticService` para reflejar ventanas activas desde `WindowManager`:

```csharp
// RuntimeDiagnosticService.cs (futura extensión)

public IReadOnlyList<WindowInfo> GetActiveWindows()
{
	// Llamar WindowManager para obtener snapshot ventanas activas
	return _windowManager.GetActiveWindowsSnapshot();
}
```

**Objetivo**: Hacer visible en Runtime Diagnostic Panel:
```
Active Windows: 3 (Detached: 2/2)
  - detached:cotizacion:42 → Cotización - Cliente ABC (opened 14:32:15)
  - detached:cotizacion:87 → Cotización - Cliente XYZ (opened 14:35:42)
  - dialog:confirmar-delete → Confirmar eliminación (opened 14:36:10)
```

**Estado actual**: NO implementado en piloto; solo funcionalidad core window management consolidado.

---

## Criterio de validación

### Build
- ✅ 0 errores compilación
- ✅ NO referencias a `IDetachedWindowManager` en codebase
- ✅ NO `new Window()` fuera de factories pasadas a `WindowManager.OpenWindow(...)`

### Runtime
1. Abrir ventana detached #1 (`detached:cotizacion:1`) → valida apertura correcta
2. Abrir ventana detached #2 (`detached:cotizacion:2`) → valida apertura correcta
3. Intentar abrir ventana #3 → valida `DetachedWindowLimitException` + mensaje operacional claro
4. Cerrar ventana #1 → valida cleanup automático (`_detachedWindowsCount` decrementa a 1)
5. Abrir ventana #3 nuevamente → valida apertura exitosa (slot liberado)
6. Abrir ventana non-detached (`dialog:confirmar`) → valida NO cuenta contra límite detached

### Observabilidad
- Logs `[WindowManager]` visibles en debug output con timing/keys correctos
- NO exceptions `ObjectDisposedException`, `NullReferenceException`, `InvalidOperationException` (excepto `DetachedWindowLimitException` esperada)
- NO leaks evidentes (ventanas zombies, handlers huérfanos)

---

## Expansión futura

### Multi-category policies
Si se necesitan límites para otras categorías de ventanas:

```csharp
// WindowManager.cs (futura extensión)

private const string DialogKeyPrefix  = "dialog:";
private const string WizardKeyPrefix  = "wizard:";
private int _dialogWindowsCount;
private int _wizardWindowsCount;
private const int MaxDialogWindows = 3;
private const int MaxWizardWindows = 1;

// En OpenWindow<TWindow, TKey>:
var isDialogWindow = internalKey.StartsWith(DialogKeyPrefix);
var isWizardWindow = internalKey.StartsWith(WizardKeyPrefix);

if (isDialogWindow && _dialogWindowsCount >= MaxDialogWindows)
	throw new WindowLimitException("dialog", MaxDialogWindows, _dialogWindowsCount);

if (isWizardWindow && _wizardWindowsCount >= MaxWizardWindows)
	throw new WindowLimitException("wizard", MaxWizardWindows, _wizardWindowsCount);
```

**Razón**: Policy enforcement sigue centralizado; convention `"category:"` prefix escala sin refactor mayor.

### Window restore positions
```csharp
// WindowOptions.cs (futura extensión)
public bool RestorePosition { get; init; } = false;

// WindowManager.cs
// Guardar última posición en settings locales, restaurar en siguiente apertura
```

### Bidirectional sync between detached windows
```csharp
// Opcional (NO prioritario): sincronizar ediciones entre ventanas detached del mismo documento
// Complejidad alta; evaluar con feedback runtime real primero
```

---

## Conclusión

**Window Management Standards (ADR-029)** formaliza `WindowManager` como **autoridad runtime centralizada obligatoria** para TODO lifecycle de ventanas en el ERP.

**Prohibición explícita** de window management manual disperso, services paralelos, tracking duplicado, y lifecycle handlers fragmentados.

**Policy enforcement consistente** (ej: máximo 2 detached windows) vive en `WindowManager` como single source of truth global runtime.

**Simplifica Pages/ViewModels**: una línea de lógica (`_windowManager.OpenWindow(...)`) reemplaza decenas de líneas duplicadas de `new Window()`, ownership, resize, activate, cleanup manual.

**Extensible por convention**: prefijos key (`"detached:"`, `"dialog:"`, `"wizard:"`) permiten policies futuras sin romper API existente.

El ERP debe sentirse **operacional**, **desktop-native**, **estable**, y **mantenible** con window management centralizado, observable, y predecible.
