# ADR-028 — Document Surface Window Detach Mode: Real Desktop Multitasking Extension

**Fecha**: 2025-01-XX  
**Estado**: ✅ IMPLEMENTADO (Window Detach Mode piloto Cotizaciones) | ⏸️ POSTPONED (Quick Preview Split Surface)  
**Impacto**: WinUI + Services + UX  
**Relacionado**: ADR-025 Document Surface UX Pattern, ADR-027 Document Surface Detachable Mode

---

## Contexto

El **Document Surface UX Pattern (ADR-025)** introdujo un modelo de edición inline limpio donde el documento reemplaza temporalmente el grid de listado (content replacement), evitando el caos de Workspace Tabs infinitos para CRUDs ligeros PYME.

El **Document Surface Detachable Mode (ADR-027)** extendió ese patrón permitiendo **split view side-by-side** opcional (grid + surface simultáneos) para multitarea ligera controlada, limitado a **1 surface desacoplada** por módulo.

Sin embargo, surgieron **dos limitaciones operacionales reales**:

1. **Pantallas pequeñas**: Split view horizontal ocupa mucho espacio; en monitores de 15" o laptops el split se siente apretado, especialmente con formularios ERP extensos como Cotizaciones (cliente, detalles, totales).

2. **Multitarea desktop real**: Usuarios con **multi-monitor** o workflows que requieren **comparar documentos**, **copiar información entre ventanas**, **consultar listado mientras edita en otra ventana**, necesitan verdadera independencia de ventanas OS, no solo split dentro de la misma ventana principal.

3. **Split view editable completo**: El split actual (ADR-027) muestra el documento completo editable en modo lateral, lo cual es pesado y no diferencia entre "quick preview read-only" y "edición multitarea real".

**Problema específico identificado**:
- Split view actual (ADR-027) es binario: desacoplado ON/OFF, pero ambos modos muestran el documento completo.
- NO existe un modo **ligero de consulta rápida** (quick preview read-only) que muestre solo información esencial sin formularios pesados.
- NO existe un modo de **ventana OS real independiente** para multitarea desktop nativa (comparar, copiar, multi-monitor).
- Workspace Tabs globales NO son solución: regresan al caos tabs infinitos, pérdida de contexto módulo, fragmentación UX institucionalizada como anti-pattern (CLAUDE_RULES.md §12).

---

## Decisión

Evolucionar el Document Surface UX Pattern hacia **tres modos oficiales** con propósitos UX específicos y restricciones arquitectónicas claras:

### Modo 1: Document Surface Inline (default — ADR-025)

**Propósito**: Edición principal PYME, operación diaria rápida.  
**Layout**: Content replacement (grid XOR surface).  
**Cuándo usar**: Flujo normal: Nuevo, Editar, CRUD ligero.  
**Estado actual**: ✅ Preservado completamente (NO tocado por ADR-028).

---

### Modo 2: Quick Preview Split Surface (read-only lateral ligero)

**Propósito**: Consulta rápida contextual mientras el grid permanece visible.  
**Layout**: Split view side-by-side (grid 40% | preview 60%).  
**Contenido**: **Resumen ligero read-only** del documento:
- Encabezado esencial (cliente, fecha, estatus, totales)
- Lista simple de detalles (sin edición inline, sin AutoSuggestBox, sin validaciones complejas)
- **NO permite edición** (botones Guardar/Agregar/Eliminar deshabilitados o ocultos)
- **NO muestra formularios complejos** (selección cliente, navegación, actions secundarias)

**Cuándo usar**:
- Comparar información entre documentos sin cerrar grid
- Revisar contenido rápido sin abrir editor completo
- Mantener visibilidad del listado mientras consulta detalles

**Restricciones obligatorias**:
- SOLO 1 Quick Preview activa por módulo (mismo límite ADR-027)
- Activación explícita mediante botón discreto "Vista Rápida" en grid o CommandBar módulo
- **NO debe ser tan pesada como el documento completo editable**
- Transición instantánea o muy sutil (NO animaciones complejas)

**Estado actual**: ⏸️ **POSTPONED** — implementación técnica intentada pero generó complejidad XAML/source-generator; **piloto pospuesto** hasta validar Window Detach Mode primero. El split actual (ADR-027) sigue mostrando documento completo editable mientras tanto.

---

### Modo 3: Window Detach Mode (ventana OS real independiente)

**Propósito**: Multitarea desktop real, monitores pequeños, comparación entre documentos, copiar información.  
**Layout**: **Window/AppWindow OS real** independiente (NO popup, NO dialog, NO flyout, NO modal, NO overlay).  
**Contenido**: Documento completo editable (misma página completa CotizacionDocumentoPage).  
**Cuándo usar**:
- Monitores pequeños (15" o menos) donde split view es apretado
- Multi-monitor: documento en pantalla secundaria, grid en primaria
- Comparación real entre documentos (2 cotizaciones lado a lado en ventanas OS separadas)
- Copiar información entre documentos
- Edición extensa mientras consulta grid en ventana principal

**Restricciones obligatorias**:
- **Máximo 2 ventanas desacopladas activas simultáneamente** (NO ilimitado)
- Activación explícita mediante botón secundario "Abrir en Ventana" (CommandBar.SecondaryCommands del documento)
- Cada ventana tiene **lifecycle UI independiente** pero comparte **misma sesión usuario**, **mismo runtime operacional**, **mismo contexto seguridad**
- Ventanas NO incluyen menú lateral ERP, navegación principal, tabs Workspace, tabs módulo, shell completo — solo el documento
- Cleanup automático al cerrar ventana (libera slot del máximo 2)
- Mensaje operacional claro cuando se intenta abrir ventana #3: *"Límite alcanzado: máximo 2 ventanas desacopladas simultáneas. Cierre alguna ventana antes de abrir otra."*

**Estado actual**: ✅ **IMPLEMENTADO** — piloto funcional en Cotizaciones, build exitoso.

---

## Implementación Window Detach Mode (ADR-028)

### Nuevos servicios

#### `IDetachedWindowManager` (contrato)

```csharp
namespace Ybridio.WinUI.Services.Workspace;

/// <summary>
/// Servicio de gestión de ventanas desacopladas (Window Detach Mode — ADR-028).
/// Controla el ciclo de vida de ventanas OS reales independientes para Document Surfaces.
/// LIMITACIÓN ARQUITECTÓNICA: Máximo 2 ventanas desacopladas activas simultáneamente.
/// </summary>
public interface IDetachedWindowManager
{
	int ActiveWindowsCount { get; }

	Task<(bool Success, string? Error, Guid? WindowId)> TryOpenDetachedWindowAsync(
		UIElement content, string title, int width = 1200, int height = 800);

	Task<bool> CloseDetachedWindowAsync(Guid windowId);
	Task CloseAllDetachedWindowsAsync();
	bool IsWindowActive(Guid windowId);
}
```

#### `DetachedWindowManager` (implementación WinUI 3 AppWindow)

- Usa `Window` + `AppWindow.GetFromWindowId(...)` para crear ventanas OS reales
- Tracking interno con `Dictionary<Guid, DetachedWindowContext>`
- Límite máximo 2: `if (_activeWindows.Count >= MaxDetachedWindows) return (false, "Límite alcanzado...", null)`
- Cleanup automático: `window.Closed += (sender, args) => _activeWindows.Remove(windowId);`
- Configuración ventana: `appWindow.Resize(new SizeInt32(width, height))`, `presenter.IsResizable = true`

**Ubicación**: `Ybridio.WinUI/Services/Workspace/DetachedWindowManager.cs`  
**DI**: `services.AddSingleton<IDetachedWindowManager, DetachedWindowManager>();` (App.xaml.cs)

---

### Cambios en CotizacionDocumentoPage (piloto)

#### XAML (`CotizacionDocumentoPage.xaml`)

```xaml
<CommandBar.SecondaryCommands>
	<!-- ADR-027: Split view lateral -->
	<AppBarButton x:Name="BtnToggleDetach" Label="Desacoplar Surface" Click="BtnToggleDetach_Click">
		<AppBarButton.Icon><FontIcon Glyph="&#xE89A;"/></AppBarButton.Icon>
	</AppBarButton>
	<!-- ADR-028: Ventana OS real -->
	<AppBarButton x:Name="BtnAbrirEnVentana" Label="Abrir en Ventana" Click="BtnAbrirEnVentana_Click">
		<AppBarButton.Icon><FontIcon Glyph="&#xE8A7;"/></AppBarButton.Icon>
	</AppBarButton>
</CommandBar.SecondaryCommands>
```

#### Code-behind (`CotizacionDocumentoPage.xaml.cs`)

```csharp
private readonly IDetachedWindowManager _detachedWindowManager;
private readonly CotizacionDto? _cotizacionOriginal; // Snapshot para ventanas detached

public CotizacionDocumentoPage(CotizacionDto? cotizacion)
{
	// ...
	_detachedWindowManager = App.Services.GetRequiredService<IDetachedWindowManager>();
	_cotizacionOriginal = cotizacion; // Guardar snapshot original
	// ...
}

private async void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
{
	var nuevaPagina = new CotizacionDocumentoPage(_cotizacionOriginal);
	var titulo = _cotizacionOriginal is not null
		? $"Cotización - {_cotizacionOriginal.NombreCliente}"
		: "Nueva Cotización";

	var resultado = await _detachedWindowManager.TryOpenDetachedWindowAsync(
		content: nuevaPagina, title: titulo, width: 1200, height: 800);

	if (!resultado.Success)
	{
		var dialog = new ContentDialog
		{
			Title = "Límite de ventanas alcanzado",
			Content = resultado.Error,
			CloseButtonText = "Entendido",
			XamlRoot = this.XamlRoot
		};
		await dialog.ShowAsync();
	}
}
```

**Razón del snapshot**: Guardar `_cotizacionOriginal` en el constructor permite crear ventanas detached con el mismo DTO inicial, **evitando conflictos de state/bindings** entre instancias de ViewModel. Cada ventana tiene su propia instancia de `CotizacionDocumentoPage` y `CotizacionDocumentoViewModel`, garantizando **lifecycle UI independiente**.

---

## Migración inicial

**Piloto SOLO en Cotizaciones** (módulo `Ventas > Cotizaciones`).

**NO migrar todavía**:
- Clientes (CRUD ligero, aún no justifica ventanas OS)
- Productos (CRUD ligero)
- Pedidos (workflow complejo, usa Workspace Tabs)
- Ventas (workflow complejo)
- OT (multi-paso, usa Workspace Tabs)

**Razón**: Validar primero:
- UX real de ventanas OS independientes vs split view
- Runtime estabilidad (lifecycle, cleanup, concurrency)
- Límite máximo 2 ventanas (suficiente o restrictivo)
- Aceptación operacional (usuarios PYME usan multi-monitor o prefieren split view)

**Criterio para expansión futura**:
1. Validación runtime exitosa (SIN leaks, SIN crashes, SIN DbContext collisions masivas)
2. Feedback UX positivo (usuarios encuentran útil modo ventana real)
3. Runtime Observability confirma overhead aceptable

---

## Concurrency runtime esperada

### Nuevos escenarios de concurrency introducidos por Window Detach Mode

1. **Múltiples instancias de ViewModel simultáneas**: 2 ventanas detached + 1 inline = hasta **3 instancias de `CotizacionDocumentoViewModel`** activas al mismo tiempo.

2. **DbContext scoped compartido**: Cada ventana OS tiene su propio árbol visual y DI scope, PERO si los servicios singleton (ej: `PermisoService`) usan `ErpDbContext` scoped, pueden surgir colisiones similares a KI-014 (DbContext concurrency).

3. **Security runtime checks convergentes**: `OnNavigatedTo` en múltiples ventanas + pre-checks de autorización + bindings runtime + refresh concurrente del Runtime Diagnostic Panel pueden provocar **múltiples evaluaciones concurrentes** de `PermisoService.TienePermisoAsync(...)`.

4. **Loaded/Closed/Activated events**: Ventanas OS tienen lifecycle propio; eventos `Window.Closed`, `Window.Activated` deben manejarse correctamente para cleanup.

5. **Autorización runtime**: Cada instancia de ViewModel debe evaluar permisos independientemente; NO cachear permisos inseguramente entre ventanas.

### Mitigación

- **Single-flight guard en `PermisoService`** (ADR-026 / KI-014 resuelto) ya mitiga evaluaciones concurrentes de permisos.
- **Cleanup automático en `DetachedWindowManager`**: `window.Closed += ...` garantiza liberación de slot al cerrar.
- **State independiente**: Cada ventana tiene su propia instancia de página/ViewModel; NO comparten state mutable.
- **Runtime Observability**: Integración futura con Runtime Diagnostic Panel para tracking ventanas activas.

**Regla preventiva**: NO debilitar Security Foundation, NO cachear autorizaciones inseguramente, NO usar `Task.Run` para aislar DbContext (anti-pattern ADR-020), mantener runtime estable/simple.

---

## Runtime Observability (integración futura)

**Pendiente**: Extender `RuntimeDiagnosticService` para reflejar:
- Cantidad de ventanas desacopladas activas
- IDs de ventanas activas
- Documento activo por ventana
- Lifecycle events (opened, closed, activated)

**Objetivo**: Hacer visible en Runtime Diagnostic Panel:
```
Detached Windows: 2/2 (LIMIT REACHED)
  - Window 1: Cotización - Cliente ABC (opened 14:32:15)
  - Window 2: Cotización - Cliente XYZ (opened 14:35:42)
```

**Estado actual**: NO implementado en piloto; solo funcionalidad core de ventanas.

---

## Anti-patterns explícitos

### ❌ Prohibido

1. **Múltiples ventanas ilimitadas**: Máximo 2 simultáneas (límite arquitectónico obligatorio).
2. **Floating popups falsas**: Usar `Window`/`AppWindow` real, NO `ContentDialog`, `Flyout`, `TeachingTip`, `Popup`.
3. **Shell ERP completo por ventana**: Ventana detached SOLO contiene el documento; NO menú lateral, NO navegación principal, NO tabs Workspace, NO tabs módulo.
4. **Navegación duplicada**: La ventana ES el workspace documento; NO intentar navegar dentro de la ventana detached a otros módulos.
5. **Edición pesada en Quick Preview split** (cuando se implemente): Quick Preview DEBE ser read-only ligera; edición completa usa inline o window detach.
6. **Desacoplar sin documento activo**: Guard obligatorio: `if (!IsDocumentSurfaceVisible) return;` (ADR-027) + validación `_cotizacionOriginal` (ADR-028).
7. **Locks tradicionales en lugar de semaphore async**: Usar `SemaphoreSlim` para serialización async, NO `lock` statement (ADR-026).

### ✅ Correcto

- Máximo 2 ventanas detached simultáneas, mensaje claro operacional al usuario
- Cleanup automático al cerrar ventana (libera slot)
- State independiente por ventana (cada instancia tiene su ViewModel propio)
- UX limpia: split view ligero para consulta rápida, ventana real para multitarea compleja
- Security runtime preservado (autorización runtime, session context compartido)

---

## UX esperado

El usuario debe poder:

1. **Operar normalmente con UX simple** (inline content replacement, flujo default preservado).
2. **Consultar información rápidamente** con Quick Preview split lateral (read-only ligero — *cuando se implemente*).
3. **Abrir documento completo en ventana real** para multitarea desktop (comparar, copiar, multi-monitor).
4. **Trabajar en pantallas pequeñas** abriendo documento en ventana completa sin perder grid.
5. **Mantener multitarea desktop limpia** con límite máximo 2 ventanas (NO caos tabs infinitos, NO ventanas ilimitadas).

El ERP debe sentirse:
- **Operacional** — flujo diario PYME sin fricción
- **Desktop-native** — ventanas OS reales, multi-monitor, familiar Windows UX
- **Moderno** — Outlook 2026 style limpio, subtle, elegant
- **Flexible** — 3 modos UX con propósitos específicos (inline, quick preview, window detach)
- **Estable** — NO leaks, NO crashes, runtime concurrency controlado

---

## Resultado validación piloto

**Build**: ✅ 0 errores (compilación exitosa post-implementación)

**Runtime validation pendiente por usuario**:
1. Abrir Cotizaciones
2. Hacer clic en "Nueva Cotización" o "Editar Cotización"
3. En CommandBar secundario, hacer clic "Abrir en Ventana"
4. Validar: ventana OS real se abre con documento completo editable
5. Abrir segunda ventana detached (máximo permitido)
6. Intentar abrir tercera ventana → validar mensaje: *"Límite alcanzado: máximo 2 ventanas desacopladas simultáneas..."*
7. Cerrar una ventana → validar cleanup automático (libera slot)
8. Trabajar en multi-monitor: grid en pantalla primaria, documento en secundaria
9. Comparar información entre 2 ventanas detached lado a lado
10. Validar: SIN exceptions DbContext, SIN leaks visibles, SIN crashes runtime

**Criterio de éxito**:
- ✅ Ventanas OS reales se abren/cierran correctamente
- ✅ Límite máximo 2 funciona y mensaje es claro
- ✅ Cleanup automático libera slots
- ✅ Multitarea real funciona (comparar, copiar, multi-monitor)
- ✅ SIN DbContext concurrency exceptions (gracias a single-flight guard ADR-026)
- ✅ Navigation/runtime estable

---

## Roadmap técnico futuro

1. **Implementar Quick Preview Split Surface** (postponed):
   - Crear `CotizacionQuickPreviewControl.xaml` UserControl ligero read-only
   - Mostrar solo: encabezado esencial, lista detalles (sin edición), totales
   - Activación: botón discreto "Vista Rápida" en grid CommandBar
   - Binding one-way desde `CotizacionResumenDto` (NO ViewModel completo)
   - Validar: overhead visual mínimo, transición instantánea

2. **Runtime Observability integration**:
   - Tracking ventanas activas en `RuntimeDiagnosticService`
   - Panel reflection: cantidad ventanas, IDs, documentos activos
   - Lifecycle events (opened, closed, activated) en contexto runtime

3. **Expandir a otros módulos** (post-validación Cotizaciones):
   - Clientes: Quick Preview + Window Detach
   - Productos: Quick Preview + Window Detach
   - Pedidos: Window Detach (workflow complejo mantiene Workspace Tabs para multi-documento)
   - Criterio: SOLO si validación piloto Cotizaciones es exitosa

4. **Optimizaciones futuras** (opcional):
   - Sincronización bidireccional entre ventanas (edición en una, refleja en otra — NO prioritario)
   - Restore window positions (guardar/restaurar posición ventanas entre sesiones — NO prioritario)
   - Limit per-module vs global (máximo 2 global o máximo 2 por módulo — evaluar con data runtime real)

---

## Conclusión

**Document Surface Window Detach Mode** es una **extensión controlada no invasiva** del Document Surface UX Pattern que preserva completamente WorkspaceService, Shell, Runtime Observability, Security Foundation, y arquitectura existente.

**UX simple por defecto** (inline content replacement) permanece intacto. Multitarea ligera controlada (split view, window detach) es **opcional bajo demanda del usuario**, evitando regresar al caos de Workspace Tabs infinitos.

**Limitación arquitectónica de máximo 2 ventanas** evita complejidad runtime excesiva, leaks, y problemas de DbContext concurrency masivos, manteniendo control lifecycle y UX operacional PYME.

**Piloto Cotizaciones** validará primero UX real, estabilidad runtime, y aceptación operacional antes de expandir a otros módulos.

El ERP debe sentirse **simple por defecto**, **poderoso opcionalmente**, y **desktop-native** sin caos visual ni complejidad innecesaria.
