# DECISIONS.md — Registro de Decisiones Arquitectónicas

> Este documento registra las decisiones técnicas importantes tomadas durante el desarrollo de Ybridio ERP,
> incluyendo la alternativa descartada y la razón de la elección.  
> Última actualización: 2026-05-09 (Document Surface UX Pattern — Reemplazar Workspace Tabs innecesarios con Document Surfaces contextuales embebidos en módulos para CRUDs ligeros)

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
