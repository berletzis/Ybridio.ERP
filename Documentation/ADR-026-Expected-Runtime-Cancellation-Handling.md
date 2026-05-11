# ADR-026 — Expected Runtime Cancellation Handling Standard

**Status:** Accepted  
**Date:** 2025  
**Context:** Ybridio ERP — WinUI Desktop Application  
**Supersedes:** None  
**Related:** ADR-025 (Security Runtime Concurrency Stabilization — `_authSemaphore`)

---

## Contexto

Durante operación normal del ERP se producen `TaskCanceledException` en:

- Navegación rápida entre módulos
- Cierre de páginas / unload lifecycle
- Re-invocación de `[RelayCommand]` cancellable (CommunityToolkit.Mvvm)
- Refresh reemplazado por invocación posterior
- Detach/close de ventanas flotantes
- Multi-window lifecycle
- Authorization runtime durante navegación concurrente

Estas excepciones **no son errores reales** — son comportamiento esperado del runtime WinUI. Sin manejo explícito, escalan a crash del proceso (`0xffffffff`) o disparan `Debugger.Break` innecesarios que interrumpen desarrollo y operación.

### Causa específica que originó este ADR

`[RelayCommand]` con `CancellationToken` en CommunityToolkit.Mvvm cancela automáticamente la invocación anterior al recibir una nueva. El token cancelado se propagaba hasta `FirstOrDefaultAsync(ct)` dentro del semáforo `_authSemaphore` en `PermisoService.TienePermisoAsync`, causando `TaskCanceledException` no capturada → crash de proceso.

---

## Decisión: Dual Strategy Oficial

Se adopta una estrategia dual explícita. **Ambas partes son requeridas**, no son alternativas.

---

### Estrategia A — `CancellationToken.None` en Authorization/Security Checks Críticos

**Scope estricto:** únicamente checks de autorización pequeños, críticos y de corta duración.

```csharp
// ErpAuthorizationService.cs
public async Task<bool> PuedeAsync(string clave, CancellationToken ct = default)
{
	if (_session.UsuarioId is not { } uid)
		return false;

	// ADR-026 — Dual Strategy A:
	// Authorization checks usan CancellationToken.None.
	// Razón: authorization parcial cancelada produce estados UX inconsistentes
	// (botones habilitados/deshabilitados incorrectamente, surfaces incompletas,
	// navegación rota). El costo del check es mínimo; el resultado es crítico.
	return await _permisos.TienePermisoAsync(uid, clave, CancellationToken.None);
}
```

**Justificación:**
- El check de permisos dura < 50ms normalmente (single-flight semaphore + EF Core indexed query).
- Una autorización parcialmente cancelada deja la UI en estado ambiguo: botones pueden aparecer habilitados/deshabilitados incorrectamente.
- El semáforo `_authSemaphore` (ADR-025) ya serializa concurrencia; añadir cancelación a mitad crea ventanas de inconsistencia.
- La seguridad debe ser **consistente o ausente**, nunca **parcialmente evaluada**.

**PROHIBIDO aplicar `CancellationToken.None` en:**
- Queries de grids y listas grandes
- Cargas de reportes
- Operaciones IO extensas
- Workflows de documentos largos
- Cualquier operación donde la cancelación sea legítimamente necesaria

---

### Estrategia B — `catch (TaskCanceledException)` Controlado en ViewModels / UI Operations

**Scope:** ViewModels con `[RelayCommand]` cancellable, operaciones de refresh UI, lifecycle de navegación.

```csharp
// Patrón estándar — ADR-026 Dual Strategy B
[RelayCommand]
public async Task RefrescarAsync(CancellationToken ct = default)
{
	IsBusy = true;
	try
	{
		// ... operación cancelable
	}
	catch (TaskCanceledException)
	{
		// ADR-026: Expected during navigation/lifecycle transitions.
		// [RelayCommand] cancels previous invocation on re-entry.
		// Not an error — no UI error message, no crash, no Debugger.Break.
	}
	finally
	{
		IsBusy = false;
	}
}
```

**Por qué `TaskCanceledException` y no `OperationCanceledException`:**
- `OperationCanceledException` es la base de `TaskCanceledException` pero también cubre flows de cancelación legítimos que queremos detectar (timeouts, cancelación explícita de usuario, etc.).
- `TaskCanceledException` es más específico al contexto de `Task` cancelada por el runtime de CommunityToolkit.Mvvm y WinUI lifecycle.
- Capturar `OperationCanceledException` masivamente puede ocultar cancellation flows legítimos.
- **Preferir `TaskCanceledException` para este contexto.**

---

## WinUI Lifecycle — Comportamiento Esperado

WinUI genera cancelaciones legítimas durante:

| Evento | Origen | Comportamiento |
|--------|--------|----------------|
| Navegación rápida | Frame.Navigate | Token de comando anterior cancelado |
| Page.Unload | Page lifecycle | Cancela operaciones async en curso |
| Window.Close | Window lifecycle | Cancela todos los tasks pendientes |
| DetachedWindow close | WindowManager | Cancela lifecycle de la ventana |
| Re-invocación RelayCommand | CommunityToolkit.Mvvm | Cancela invocación previa automáticamente |
| Refresh overlap | ViewModel | Segunda invocación cancela primera |

**Todas estas son `expected runtime cancellations` — NO errores operacionales.**

---

## Runtime Observability — Clasificación Oficial

El sistema de observabilidad (`OperationalObservabilityService`) debe distinguir:

### Expected Runtime Cancellation (Nivel: Trace/Debug)
```
[RUNTIME-CANCEL] Expected cancellation: {operacion} during {contexto}
```
- NO dispara `Debugger.Break`
- NO aparece como error en Runtime Observability panel
- NO cuenta como falla operacional
- Registro en nivel `Debug` / `Trace` únicamente

### Unhandled Runtime Failure (Nivel: Error/Critical)
```
[RUNTIME-ERROR] Unhandled failure: {excepcion} in {contexto}
```
- SÍ puede disparar `Debugger.Break` si corresponde
- SÍ aparece como error en Runtime Observability
- SÍ cuenta como falla operacional

### Regla de clasificación:
```
TaskCanceledException donde ct.IsCancellationRequested == true
	→ Expected Runtime Cancellation (Trace)

TaskCanceledException donde ct.IsCancellationRequested == false
	→ Posible error real → investigar (Warning/Error)

Cualquier otra Exception no esperada
	→ Unhandled Runtime Failure (Error/Critical)
```

---

## Anti-Patterns Oficiales — PROHIBIDO

| Anti-pattern | Razón |
|---|---|
| `catch (Exception) { }` silencioso | Oculta errores reales, viola Security Foundation |
| `CancellationToken.None` global/masivo | Hace inoperables los mecanismos de cancelación legítima |
| `catch (OperationCanceledException)` masivo | Demasiado amplio, oculta cancellation flows legítimos |
| Tratar toda `TaskCanceledException` como crash | Genera falsos positivos, crashes innecesarios |
| Tratar toda `TaskCanceledException` como ignorable | Puede ocultar errores reales de DB/network |
| `Debugger.Break` en expected cancellations | Interrumpe desarrollo/operación innecesariamente |
| Suprimir excepciones en capas de seguridad | Viola Security Foundation — nunca |
| Remover `_authSemaphore` single-flight | Viola ADR-025 — nunca |

---

## Scope de Aplicación

### APLICA (Estrategia B — catch controlado):
- Todos los ViewModels con `[RelayCommand]` + `CancellationToken` que ejecutan operaciones async
- Operaciones de refresh/cargar listas (RefrescarAsync, CargarAsync, etc.)
- Preview lifecycle en document surfaces
- Window detach/close lifecycle operations
- NavigationService operations cancelables

### APLICA (Estrategia A — `CancellationToken.None`):
- `ErpAuthorizationService.PuedeAsync` → `TienePermisoAsync`
- Scope validation checks en security guards
- UI authorization visibility checks (show/hide basado en permisos)
- Runtime security guards de corta duración

### NO APLICA `CancellationToken.None`:
- `VentaDocumentalService.ListarAsync` (query de lista — debe ser cancelable)
- `FinanzasService` queries
- `InventarioService` queries
- Cualquier operación que el usuario pueda cancelar explícitamente
- Reportes y exports

---

## Validación

Se considera implementado correctamente cuando:

- [ ] Navegación rápida entre módulos no produce crashes
- [ ] Re-invocación de RefrescarAsync no dispara Debugger.Break
- [ ] Authorization checks completan consistentemente durante navegación
- [ ] `OperationalObservabilityService` muestra expected cancellations en nivel Trace (no Error)
- [ ] Botones y permisos no quedan en estado inconsistente durante navegación rápida
- [ ] Multi-window detach/close no genera exceptions no manejadas
- [ ] Process exit code sigue siendo `0` tras navegación normal

---

## Consecuencias

**Positivas:**
- Navegación WinUI estable y fluida
- Sin crashes innecesarios por comportamiento esperado
- Observabilidad limpia — solo errores reales en nivel Error
- Seguridad consistente — authorization siempre completa
- Developer experience mejorado — sin Debugger.Break falsos

**A monitorear:**
- `CancellationToken.None` en auth no debe expandirse fuera de su scope
- `catch (TaskCanceledException)` debe tener comentario explicativo en cada uso
- Si una `TaskCanceledException` aparece donde `ct.IsCancellationRequested == false`, investigar — puede ser error real

---

## Referencias

- ADR-025: Security Runtime Concurrency Stabilization (`_authSemaphore`)
- `PermisoService.TienePermisoAsync` — single-flight guard
- `ErpAuthorizationService.PuedeAsync` — authorization entry point
- CommunityToolkit.Mvvm `[RelayCommand]` cancellation behavior
- `OperationalObservabilityService` — runtime observability
