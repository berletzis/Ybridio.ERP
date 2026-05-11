# ADR-030 — Global Document Surface UX Migration: Eliminación Tabs Ensimados + Navegación Desktop-Native

**Fecha**: 2026-05-10  
**Estado**: ✅ Implementado — Fases 1-2 (Clientes, Productos)  
**Responsable**: Arquitectura ERP  
**Decisión**: Migración oficial global

---

## Contexto

### Problema identificado

El ERP tenía un **problema arquitectónico UX** que afectaba la experiencia operacional diaria:

#### Tabs ensimados y navegación pesada

- **Tabs documentales innecesarios**: CRUDs simples (nueva/editar cliente, producto, cotización) abrían **tabs workspace persistentes**, generando acumulación visual y navegación fragmentada
- **Overlap visual**: tabs dentro de tabs dentro de tabs producían jerarquía UX confusa, pareciendo "IDE/browser con tabs infinitos" en lugar de ERP operacional PYME
- **Profundidad navegación excesiva**: `Módulo → Tab Workspace → Formulario → Guardar → Cerrar Tab → Buscar Módulo → ...` generaba fricción operacional diaria
- **UX inconsistente**: algunos módulos usaban tabs workspace para todo, otros usaban Document Surface Pattern (ADR-025 Cotizaciones piloto), creando experiencia fragmentada
- **Caos Workspace**: acumulación de tabs CRUD simples saturaba workspace tabs con documentos operacionales triviales, ocultando workflows verdaderamente importantes (análisis, OT complejas, multi-documento)

#### Navegación desktop NO aprovechada

- **Ausencia quick preview**: NO existía modo consulta rápida read-only para revisar información sin formularios pesados
- **Ausencia multi-window desktop**: NO existía capacidad de abrir documentos en ventanas OS reales para multitarea desktop-native (multi-monitor, comparación lado a lado)
- **Split view limitado**: solo Cotizaciones tenía split view (ADR-027) y Window Detach Mode (ADR-028), otros módulos NO

#### Resultado operacional

- **Caos visual**: tabs ensimados producían confusión navegacional diaria
- **Fricción operativa**: navegación pesada workspace tabs ralentizaba operación PYME rápida
- **UX desktop anticuada**: el ERP parecía aplicación web browser-based, NO aplicación desktop-native Windows moderna
- **Experiencia fragmentada**: diferentes módulos tenían diferentes patrones UX documentales

---

## Decisión

**Migrar oficialmente TODOS los módulos compatibles al patrón unificado**:

```
Document Surface Pattern (ADR-025 + ADR-027 + ADR-028 + ADR-029)
```

### Tres modos UX oficiales

1. **Inline Content Replacement** (default)
   - Document Surface embebido reemplaza grid listado temporalmente
   - Modo normal para operación diaria PYME rápida
   - Grid XOR surface, NO simultáneos
   - Preserva simplicidad operacional

2. **Quick Preview Split Surface** (postponed scope futuro)
   - Split lateral read-only ligero para consulta rápida
   - Mantiene grid visible mientras muestra información esencial
   - SIN formularios completos, solo datos clave
   - Multitarea ligera contextual

3. **Window Detach Mode** (implementado ADR-028+029)
   - Ventana OS real independiente usando WindowManager (ADR-029)
   - Multitarea desktop-native (multi-monitor, comparación, copiar datos)
   - Límite máximo 2 detached windows policy global
   - Desktop-native profesional

### Workspace Tabs reservados SOLO para

- Workflows largos/complejos (OT multi-paso, diseño → producción → QA)
- Análisis operacional persistente
- Multi-documento workflows avanzados
- Escenarios que requieren MÚLTIPLES documentos abiertos simultáneamente de forma persistente

### Módulos migrados

#### Fase 1 — Clientes (crítico operacional)
- ✅ ClientesPage → ClienteDocumentoPage
- ✅ Document Surface Inline + Window Detach Mode
- ✅ WindowManager integration

#### Fase 2 — Productos (inventario core)
- ✅ ProductosPage → ProductoDocumentoPage
- ✅ Document Surface Inline + Window Detach Mode
- ✅ WindowManager integration

#### Fase 3 — Administrativa (postponed validación fases previas)
- 🔲 Catálogos (Proveedores, Unidades, Categorías)
- 🔲 Configuraciones administrativas

#### NO migrar todavía (workflows complejos)
- ❌ Pedidos (multi-documento, genera Ventas, workflow complejo)
- ❌ Ventas (genera CxC, descuento inventario, multi-documento)
- ❌ OT (multi-paso: diseño → producción → QA → entregada)
- ❌ Dashboards, Reportes, Análisis

---

## Arquitectura implementada

### XAML Pattern

Estructura layout dual mode (normal/detached):

```xaml
<Grid Grid.Row="2">
	<Grid.ColumnDefinitions>
		<!-- Dinámicas según IsDocumentSurfaceDetached -->
		<ColumnDefinition x:Name="ListadoColumn" Width="*"/>
		<ColumnDefinition x:Name="SplitterColumn" Width="0"/>
		<ColumnDefinition x:Name="SurfaceColumn" Width="0"/>
	</Grid.ColumnDefinitions>

	<!-- Grid listado: visible cuando IsDocumentSurfaceVisible=false -->
	<Border Grid.Column="0"
			Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay, 
								  Converter={StaticResource InverseBoolToVisibilityConverter}}">
		<ListView ItemsSource="{x:Bind ViewModel.Items, Mode=OneWay}" ... />
	</Border>

	<!-- Document Surface: ColumnSpan=3 en modo normal, Column=2 en detached -->
	<ContentPresenter Grid.Column="0" Grid.ColumnSpan="3"
					  Content="{x:Bind ViewModel.DocumentSurfaceContent, Mode=OneWay}"
					  Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay}"/>
</Grid>
```

### ViewModel Pattern

Properties Document Surface obligatorias:

```csharp
/// <summary>
/// Indica si el Document Surface está visible (reemplaza grid listado).
/// </summary>
[ObservableProperty] private bool isDocumentSurfaceVisible;

/// <summary>
/// Contenido del Document Surface (instancia Page embebida o null).
/// </summary>
[ObservableProperty] private object? documentSurfaceContent;

/// <summary>
/// Indica si el Document Surface está en modo desacoplado (split view).
/// false (default) = Content Replacement (grid XOR surface)
/// true = Detached Mode (grid + surface simultáneos side-by-side)
/// </summary>
[ObservableProperty] private bool isDocumentSurfaceDetached;

public void AbrirNuevoDocumento()
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
	await RefrescarAsync(); // Refrescar grid
}

[RelayCommand]
public void ToggleDetach()
{
	if (!IsDocumentSurfaceVisible) return; // Guard obligatorio
	IsDocumentSurfaceDetached = !IsDocumentSurfaceDetached;
}
```

### Code-behind DocumentoPage Pattern

Callbacks y Window Detach Mode:

```csharp
/// <summary>
/// Callback invocado cuando usuario hace clic en "← Volver a Lista".
/// </summary>
public Action? VolverALista { get; set; }

/// <summary>
/// Callback invocado cuando usuario hace clic en "Desacoplar Surface".
/// </summary>
public Action? ToggleDetach { get; set; }

private void BtnVolverALista_Click(object sender, RoutedEventArgs e)
{
	VolverALista?.Invoke();
}

private void BtnToggleDetach_Click(object sender, RoutedEventArgs e)
{
	ToggleDetach?.Invoke();
}

// ── Window Detach Mode (ADR-028 + ADR-029) ───────────────────────────────

private async void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
{
	var titulo = _documentoOriginal is not null
		? $"{Modulo} - {_documentoOriginal.Nombre}"
		: $"Nuevo {Modulo}";

	var detachedKey = $"detached:{modulo}:{id}";

	try
	{
		// ADR-029: WindowManager centralizado con key convention "detached:"
		_windowManager.OpenWindow<DetachedDocumentWindow, string>(
			key: detachedKey,
			factory: () =>
			{
				var nuevaPagina = new DocumentoPage(_documentoOriginal);
				return new DetachedDocumentWindow(nuevaPagina, titulo);
			},
			options: new WindowOptions { Width = 1200, Height = 800 });
	}
	catch (DetachedWindowLimitException ex)
	{
		// Mostrar ContentDialog operacional claro
		await MostrarMensajeLimiteVentanasAsync(ex);
	}
}

private async Task MostrarMensajeLimiteVentanasAsync(DetachedWindowLimitException ex)
{
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

### Code-behind ModulePage Pattern

Wire callbacks:

```csharp
private void BtnNuevo_Click(object sender, RoutedEventArgs e)
{
	var page = new DocumentoPage(null);
	page.ViewModel.DocumentSaved = OnDocumentSaved;
	page.VolverALista = OnVolverALista;
	page.ToggleDetach = OnToggleDetach;
	ViewModel.DocumentSurfaceContent = page;
	ViewModel.IsDocumentSurfaceVisible = true;
}

private void OnDocumentSaved()
{
	_ = ViewModel.CerrarDocumentSurfaceAsync();
}

private void OnVolverALista()
{
	_ = ViewModel.CerrarDocumentSurfaceAsync();
}

private void OnToggleDetach()
{
	ViewModel.ToggleDetachCommand.Execute(null);
}
```

---

## Anti-patterns oficiales PROHIBIDOS

### ❌ NO tabs workspace para CRUDs simples

```csharp
// ❌ PROHIBIDO: abrir tabs workspace para nueva/editar documento CRUD simple
_workspace.OpenTab("nuevo-cliente", "Nuevo Cliente", typeof(ClientePage), null);

// ✅ CORRECTO: usar Document Surface Pattern
ViewModel.AbrirNuevoDocumento();
```

### ❌ NO ventanas manuales (usar WindowManager)

```csharp
// ❌ PROHIBIDO: new Window() fuera de WindowManager
var window = new Window { Title = "Detalle Cliente" };
var hwnd = WindowNative.GetWindowHandle(window);
// ...

// ✅ CORRECTO: WindowManager centralizado (ADR-029)
_windowManager.OpenWindow<DetachedDocumentWindow, string>(
	key: $"detached:cliente:{id}",
	factory: () => new DetachedDocumentWindow(page, titulo),
	options: new WindowOptions { Width = 1200, Height = 800 });
```

### ❌ NO múltiples surfaces desacopladas simultáneas (límite 1 por módulo)

```csharp
// ❌ PROHIBIDO: permitir múltiples Document Surfaces desacopladas en el mismo módulo
if (DetachedSurfacesCount > 1) // NO implementar esto

// ✅ CORRECTO: solo 1 Document Surface desacoplada por módulo (ADR-027)
// Window Detach Mode (ADR-028) permite hasta 2 ventanas OS reales total ERP
```

### ❌ NO split view permanente por defecto

```csharp
// ❌ PROHIBIDO: IsDocumentSurfaceDetached = true por defecto
public void AbrirNuevo()
{
	IsDocumentSurfaceDetached = true; // PROHIBIDO: debe ser false
}

// ✅ CORRECTO: content replacement por defecto
public void AbrirNuevo()
{
	IsDocumentSurfaceDetached = false; // Default: simple UX
}
```

### ❌ NO migrar workflows complejos

```csharp
// ❌ PROHIBIDO: migrar Pedidos/Ventas/OT a Document Surface
// Estos módulos tienen workflows multi-documento complejos que REQUIEREN
// tabs workspace persistentes para mantener contexto operacional

// ✅ CORRECTO: Document Surface SOLO para CRUDs ligeros
// Clientes, Productos, Catálogos, Configuraciones
```

---

## UX Principles institucionalizados

### Simple por defecto, poderoso opcionalmente

- **Default**: content replacement (grid XOR surface), operación diaria rápida PYME
- **Opcional**: split view detached (botón secundario), multitarea ligera bajo demanda
- **Avanzado**: Window Detach Mode (botón secundario), desktop-native multi-monitor

### Desktop-native profesional

- Ventanas OS reales con ownership Win32 (ADR-029)
- Multi-monitor support natural
- Z-order garantizado sobre MainWindow
- Límite máximo 2 detached windows (policy global)

### Navegación limpia operacional

- **Menos tabs**: CRUDs simples NO generan tabs workspace innecesarios
- **Contexto preservado**: Document Surface mantiene usuario en módulo original
- **Flujo PYME**: `crear → guardar → cerrar automático → seguir trabajando`
- **Workspace tabs para workflows importantes**: análisis, OT, multi-documento persistente

### Outlook 2026 aesthetic

- Clean minimalist style preservado
- NO enterprise dock managers complejos
- NO floating windows caóticas
- Split view limpio side-by-side cuando necesario

---

## Runtime considerations

### Security Runtime Concurrency (ADR-026 compatible)

- Nuevas Document Surfaces embebidas generan `OnNavigatedTo` + pre-checks autorización concurrentes
- Single-flight pattern `PermisoService._authSemaphore` serializa evaluaciones DbContext
- Compatible con navegación rápida nueva/editar/abrir documentos
- SIN exceptions `System.InvalidOperationException: "A second operation was started on this context instance"`

### Runtime Observability (compatible)

- `ICurrentContextTracker` refleja Document Surface activo correctamente
- Runtime Diagnostic Panel muestra módulo + documento + lifecycle
- `IOperationalObservabilityService` reporta contexto embebido sin overhead

### Performance

- Document Surfaces embebidos son pages WinUI ligeras, NO tabs workspace pesados
- Window Detach Mode límite máximo 2 evita overhead memoria excesiva
- Cleanup automático `window.Closed` handler en WindowManager (ADR-029)

### State Preservation

- Filtros, scroll, selección grid mantienen durante Document Surface lifecycle
- Toggle detach NO pierde estado grid listado
- Window Detach Mode usa snapshot DTO original para evitar conflictos state

---

## Migración gradual obligatoria

### Fase 1 — Clientes ✅ (crítico operacional)

Migrado 2026-05-10:
- ClientesPage → ClienteDocumentoPage
- Document Surface Inline + Window Detach Mode
- WindowManager integration
- Eliminados workspace tabs CRUD simple

### Fase 2 — Productos ✅ (inventario core)

Migrado 2026-05-10:
- ProductosPage → ProductoDocumentoPage
- Document Surface Inline + Window Detach Mode
- WindowManager integration
- Eliminados workspace tabs CRUD simple

### Fase 3 — Administrativa (postponed validación runtime)

Pendiente validación exhaustiva Fases 1-2:
- Catálogos (Proveedores, Unidades, Categorías, Familias)
- Configuraciones administrativas ligeras

**NO expandir sin validación runtime estable Clientes + Productos**

---

## Validación runtime requerida

### Escenarios críticos

1. **Navegación rápida CRUD**
   - Nueva/Editar/Abrir documento repetidamente
   - Confirmar NO DbContext concurrency exceptions
   - Confirmar state preservation filtros/scroll

2. **Window Detach Mode**
   - Abrir ventana detached → límite máximo 2 enforced
   - Intentar tercera → ContentDialog claro usuario
   - Cerrar ventana → cleanup automático + decremento contador

3. **Toggle Detach Split View**
   - Detach surface → grid + surface simultáneos
   - Attach surface → content replacement restaurado
   - Confirmar NO overlap visual, layout responsive

4. **Multi-monitor desktop**
   - Window Detach Mode en múltiples monitores
   - Confirmar z-order correcto, ownership Win32
   - Confirmar NO ventanas zombies después cerrar

5. **Runtime Observability**
   - Runtime Diagnostic Panel refleja Document Surface activo
   - Módulo + documento + lifecycle correcto
   - SIN overhead runtime perceptible

---

## Resultado esperado

El ERP debe evolucionar hacia:

✅ **UX operacional moderna** — Document Surface Pattern unificado global  
✅ **Desktop-native workflow** — Window Detach Mode multi-monitor profesional  
✅ **Navegación limpia** — SIN tabs ensimados, SIN caos workspace  
✅ **Menos fricción operacional** — flujo PYME rápido preservado  
✅ **Multitarea controlada** — split view + detached windows bajo demanda  
✅ **Experiencia consistente** — mismo patrón UX en todos los módulos compatibles  
✅ **Mayor foco operacional** — workspace tabs reservados workflows importantes  

El sistema debe sentirse:
- **Estable** — SIN regresiones runtime críticas
- **Moderno** — desktop-native Windows profesional
- **Profesional** — Outlook 2026 aesthetic limpio
- **ERP-like** — operacional PYME, NO IDE/browser tabs infinitos
- **Usable diariamente** — navegación fluida, rápida, clara

---

## Referencias

- **ADR-025**: Document Surface UX Pattern (piloto Cotizaciones)
- **ADR-027**: Document Surface Detachable Mode (split view opcional)
- **ADR-028**: Document Surface Window Detach Mode (ventanas OS reales)
- **ADR-029**: Window Management Standards (WindowManager single source of truth)
- **ADR-026**: Security Runtime Concurrency Stabilization (single-flight pattern)
- **CLAUDE_RULES.md**: §3 NO Rehacer WorkspaceService, §13 Window Management Standards
- **ARCHITECTURE_STATUS.md**: Estado módulos migrados Document Surface
- **KNOWN_ISSUES.md**: KI-015 Límite detached windows policy

---

**Última actualización**: 2026-05-10  
**Próxima revisión**: Después validación runtime Fase 1+2 estable  
**Decisión final**: Arquitectura UX oficial globalizada, expandir Fase 3 según validación
