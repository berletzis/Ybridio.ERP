# ADR-031 — Document Surface Visual Separation Standard

**Fecha**: 2026-05-10  
**Estado**: ✅ Implementado — Pedidos, OT, Ventas Documentales migrados  
**Responsable**: Arquitectura ERP  
**Relacionado**: ADR-025 Document Surface UX Pattern, ADR-027 Detachable Mode, ADR-028 Window Detach Mode, ADR-029 Window Management Standards, ADR-030 Global Document Surface UX Migration

---

## Problema

El ERP mostraba un **anti-pattern visual crítico** identificado en Pedidos, Órdenes de Trabajo y Ventas Documentales:

```
[Clientes] [Cotizaciones] [Pedidos ×] [Ventas] [OT]   ← tabs módulo (navegación)
							  ↓
					 [Nuevo Pedido ×]                  ← tab workspace documental
```

El usuario percibía **dos capas de tabs**:
- Tabs de módulo (navegación principal del ERP)
- Tabs workspace para documentos CRUD simples (pedido nuevo, OT, venta)

### Efectos negativos

| Síntoma | Impacto |
|---|---|
| Apariencia browser/IDE | ERP parecía Chrome o Visual Studio, no aplicación ERP PYME |
| Doble jerarquía UX | Tabs de módulo y tabs documentales al mismo nivel visual |
| Transparencia/overlay | Tabs documentales translúcidos sobre tabs de módulo |
| Línea azul de selección | Apariencia de tab seleccionado activo en documentos CRUD |
| Close ×  en documento | Los CRUDs parecían tabs adicionales, no documentos |
| Acumulación de tabs | WorkspaceService acumulaba tabs CRUD triviales mezclados con workflows reales |
| Fricción operacional | Flujo: Módulo → Tab Workspace → Documento → Cerrar Tab → Buscar módulo → ... |

---

## Decisión

**Formalizar oficialmente la separación visual y funcional entre navegación de módulo y documento activo.**

### Jerarquía UX oficial

```
1. Tabs módulo (WinUI NavigationView / TabView de módulos)
	   — representan navegación principal
	   — compactos, contextuales al módulo
	   — NUNCA contienen documentos CRUD simples

		↓

2. Document Surface Header (operacional)
	   — título del documento activo
	   — estatus del documento (badge)
	   — breadcrumb ligero: "Módulo > Título"
	   — botón "volver al listado"
	   — SIN línea azul activa, SIN close ×, SIN apariencia browser

		↓

3. Toolbar operacional (CommandBar del documento)
	   — acciones del documento: Guardar, Agregar Línea, Avanzar Estado, etc.

		↓

4. Contenido operacional (formulario / grid de detalles)
```

### Modo de operación para CRUDs simples

**Inline Content Replacement** (modo default):
- El documento reemplaza **temporalmente** el grid de listado
- `IsDocumentSurfaceVisible = true` → listado oculto, `ContentPresenter` visible
- `IsDocumentSurfaceVisible = false` → listado visible, surface oculta
- Botón "Volver" cierra el surface y refresca el listado automáticamente
- **SIN** nuevo tab en el Workspace
- **SIN** acumulación de tabs

### Workspace tabs — uso reservado

`IWorkspaceService.OpenTab` / `OpenOrActivateDocumentTabAsync` quedan **reservados exclusivamente** para:
- Workflows complejos multi-paso (OT: diseño → producción → QA)
- Análisis operacional persistente que el usuario necesita mantener abierto
- Multi-documento workflows que requieren comparar documentos simultáneamente
- Navegación cruzada entre documentos relacionados (ej: abrir Pedido origen desde una Venta)

---

## Patrón de implementación

### Host Page (lista)

```xaml
<!-- Grid listado — oculto cuando surface está activa -->
<Border Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay,
		Converter={StaticResource InverseBoolToVisibilityConverter}}">
	<ListView .../>
</Border>

<!-- Document Surface — visible cuando surface está activa -->
<ContentPresenter
	Content="{x:Bind ViewModel.DocumentSurfaceContent, Mode=OneWay}"
	Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay,
				 Converter={StaticResource BoolToVisibilityConverter}}"/>
```

### Host ViewModel (estado de surface)

```csharp
// Propiedades de surface
public bool    IsDocumentSurfaceVisible  { get; set; }
public object? DocumentSurfaceContent   { get; set; }
public bool    IsDocumentSurfaceDetached { get; set; }

// Apertura inline — SIN workspace tab
public void AbrirDocumento(object page)
{
	DocumentSurfaceContent   = page;
	IsDocumentSurfaceVisible = true;
}

// Cierre y regreso al listado
public async Task CerrarDocumentSurfaceAsync()
{
	IsDocumentSurfaceVisible = false;
	DocumentSurfaceContent   = null;
	await RefrescarCommand.ExecuteAsync(null);
}
```

### Host Code-Behind

```csharp
private void BtnNuevo_Click(object sender, RoutedEventArgs e)
{
	// ADR-031: inline, NO workspace tab
	var page = new MiDocumentoPage(null);
	page.OnCerrar = async () => await ViewModel.CerrarDocumentSurfaceAsync();
	ViewModel.AbrirDocumento(page);
}
```

### Document Page (OnCerrar + header visual)

```csharp
/// <summary>Callback para volver al listado (ADR-031).</summary>
public Func<Task>? OnCerrar { get; set; }

private async void BtnVolver_Click(object sender, RoutedEventArgs e)
{
	if (OnCerrar is not null) await OnCerrar();
}
```

```xaml
<!-- ADR-031: Document Surface Header — SIN apariencia tab -->
<Border Background="{ThemeResource LayerFillColorDefaultBrush}"
		BorderBrush="#E5E5E5" BorderThickness="0,0,0,1" Padding="12,8,20,8">
	<StackPanel Orientation="Horizontal" Spacing="8">
		<Button Click="BtnVolver_Click" Style="{StaticResource TransparentButtonStyle}"
				ToolTipService.ToolTip="Volver al listado">
			<FontIcon Glyph="&#xE76B;" FontSize="14"/>
		</Button>
		<TextBlock Text="Módulo" FontSize="12"
				   Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
		<TextBlock Text="›" FontSize="12"
				   Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
		<TextBlock Text="{x:Bind ViewModel.TituloDocumento, Mode=OneWay}"
				   FontSize="15" FontWeight="SemiBold"/>
		<Border Background="{ThemeResource AccentFillColorDefaultBrush}"
				CornerRadius="4" Padding="8,2,8,2">
			<TextBlock Text="{x:Bind ViewModel.EstatusTexto, Mode=OneWay}"
					   Foreground="White" FontSize="11" FontWeight="SemiBold"/>
		</Border>
	</StackPanel>
</Border>
```

---

## Anti-patterns oficiales — PROHIBIDO

| Anti-pattern | Por qué está prohibido |
|---|---|
| `_workspace.OpenTab(...)` para CRUDs simples (Nuevo Pedido, Nueva OT, Nueva Venta) | Genera tabs documentales que se confunden con tabs de módulo |
| `OpenOrActivateDocumentTabAsync(...)` para documentos simples | Mismo problema; reservado para navegación cruzada compleja |
| Document Surface con línea azul de selección activa | Parece tab seleccionado de browser |
| Document Surface con close × estilo tab | Parece tab adicional, no documento operacional |
| Document Surface con transparencia o overlay sobre tabs de módulo | Ensimamiento visual que confunde la jerarquía |
| Headers tipo browser (URL, histórico, navegación hacia atrás tipo Chrome) | ERP desktop-native, no aplicación web |
| Workspace tabs para formularios de alta/edición simples | Anti-pattern documentado; usar inline content replacement |
| Múltiples tabs CRUD acumulados en workspace | Satura el workspace y oculta workflows reales |

---

## Módulos migrados (estado)

| Módulo | Anti-pattern eliminado | Modo actual |
|---|---|---|
| **Clientes** | ✅ Tab workspace eliminado | Inline Document Surface (ADR-030 Fase 1) |
| **Productos** | ✅ Tab workspace eliminado | Inline Document Surface (ADR-030 Fase 2) |
| **Cotizaciones** | ✅ Tab workspace nunca fue el modo primario | Inline + Detachable + Window Detach (ADR-025/027/028) |
| **Pedidos** | ✅ Tab workspace eliminado (ADR-031) | Inline Document Surface |
| **Órdenes de Trabajo** | ✅ Tab workspace eliminado (ADR-031) | Inline Document Surface |
| **Ventas Documentales** | ✅ Tab workspace eliminado (ADR-031) | Inline Document Surface |

---

## Separación visual obligatoria

| Elemento | Requisito visual |
|---|---|
| Tabs de módulo | Compactos, solo navegación, NO contienen documentos |
| Document Surface Header | Breadcrumb ligero, fondo diferenciado (`LayerFillColorDefaultBrush`), borde inferior sutil |
| Toolbar documento | CommandBar operacional con acciones del documento |
| Contenido documento | Surface limpia, sin transparencia, sin overlap visual |
| Botón volver | Flecha `←` discreta, tooltip "Volver al listado", no un close × |

---

## Window Detach Mode (complementario)

Las ventanas desacopladas (`WindowManager.OpenWindow<DetachedDocumentWindow, string>(...)`) **sí pueden** mostrar el Document Surface Header completo porque la ventana OS **es** el workspace del documento, y la jerarquía ya no se confunde con tabs de módulo.

Ver ADR-028 y ADR-029 para políticas de Window Detach Mode.

---

## Quick Preview (futuro)

Quick Preview (ADR-028 Modo 2, postponed) cuando se implemente **deberá** verse como surface contextual ligera, **NO** como tab documental ni como documento completo editable.

---

## Razonamiento desktop-native

El ERP Ybridio es una **aplicación WinUI desktop-native** orientada a operación PYME. Sus usuarios:
- Operan rápido con flujos CRUD frecuentes (crear pedido, agregar OT, registrar venta)
- Necesitan distinguir inmediatamente qué están viendo (navegación de módulo vs documento activo)
- No necesitan, ni esperan, una experiencia tipo browser con tabs infinitos
- Valoran una UX limpia, operacional y predecible — Outlook 2026 style

Los tabs de workspace tienen costo cognitivo: el usuario debe rastrear qué tabs tiene abiertos, cuáles ya procesó y cuáles descartar. Para CRUDs simples ese costo es innecesario y es fuente diaria de fricción operacional.

---

## Archivos afectados

| Archivo | Cambio |
|---|---|
| `Ybridio.WinUI/Views/Ventas/PedidosPage.xaml` + `.cs` | Migrado a inline Document Surface; eliminado `_workspace.OpenTab` |
| `Ybridio.WinUI/Views/Ventas/PedidoDocumentoPage.xaml` + `.cs` | Header operacional ADR-031; `OnCerrar` callback |
| `Ybridio.WinUI/ViewModels/Ventas/PedidosViewModel.cs` | Estado surface: `IsDocumentSurfaceVisible`, `DocumentSurfaceContent` |
| `Ybridio.WinUI/Views/Ventas/OrdenesTrabajoPage.xaml` + `.cs` | Migrado a inline Document Surface; eliminado workspace tab |
| `Ybridio.WinUI/Views/Ventas/OrdenTrabajoDocumentoPage.xaml` + `.cs` | Header operacional ADR-031; `OnCerrar` callback |
| `Ybridio.WinUI/ViewModels/Ventas/OrdenesTrabajoViewModel.cs` | Estado surface inline |
| `Ybridio.WinUI/Views/Ventas/VentasDocumentalesPage.xaml` + `.cs` | Migrado a inline Document Surface; eliminado workspace tab |
| `Ybridio.WinUI/Views/Ventas/VentaDocumentoPage.xaml` + `.cs` | Header operacional ADR-031; `OnCerrar` callback |
| `Ybridio.WinUI/ViewModels/Ventas/VentasDocumentalesViewModel.cs` | Estado surface inline |
| `Documentation/ADR-031-*.md` | Este documento |
| `Documentation/CLAUDE_RULES.md` | Nueva regla §12 anti-patterns tabs documentales |
| `Documentation/DECISIONS.md` | Registro de esta decisión |
| `Documentation/ARCHITECTURE_STATUS.md` | Estado de migración actualizado |
