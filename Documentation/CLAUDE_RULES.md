# Ybridio ERP — Master Architecture Rules for Claude Code

> Estas reglas aplican para TODOS los requerimientos futuros del proyecto.  
> Claude Code debe leer y respetar este documento ANTES de implementar cualquier cambio.  
> Estas reglas son **permanentes** y forman parte oficial de la arquitectura del ERP.  
> Última actualización: 2026-05-13 (ADR-050→ADR-056: Config Global, Folios, Tax Pattern, Charges, Scroll, Ownership)

---

## 0b-ext1. Single Document Scroll Pattern (ADR-055)

**OBLIGATORIO en TODAS las superficies documentales del ERP.**

### Anti-patrones PROHIBIDOS

```xml
<!-- ❌ PROHIBIDO: Height="*" en fila de grids documentales → scroll interno -->
<RowDefinition Height="*"/>
<ListView .../>  <!-- ListView captura el scroll con Height="*" -->

<!-- ❌ PROHIBIDO: MaxHeight arbitrario en secciones documentales -->
<Border MaxHeight="200">
    <ListView .../>  <!-- Se clipa y genera scroll interno -->
</Border>

<!-- ❌ PROHIBIDO: Nested ScrollViewers -->
<ScrollViewer>
    <ListView .../>  <!-- ListView tiene su propio ScrollViewer interno -->
</ScrollViewer>
```

### Estructura obligatoria en superficies documentales

```xml
<!-- ✅ CORRECTO: ScrollViewer como único dueño del scroll -->
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>  <!-- CommandBar fijo arriba -->
        <RowDefinition Height="*"/>     <!-- ScrollViewer — único scroll owner -->
        <RowDefinition Height="Auto"/>  <!-- StatusBar fijo abajo -->
    </Grid.RowDefinitions>

    <CommandBar Grid.Row="0" .../>

    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
        <Grid>
            <!-- TODAS las filas internas en Auto — crecen con contenido -->
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <!-- etc. -->
            </Grid.RowDefinitions>

            <!-- ListView con scroll DESACTIVADO — crece dinámicamente -->
            <ListView ScrollViewer.VerticalScrollBarVisibility="Disabled"
                      ScrollViewer.VerticalScrollMode="Disabled" .../>
        </Grid>
    </ScrollViewer>

    <Border Grid.Row="2" Style="{StaticResource ErpGridStatusBarStyle}" .../>
</Grid>
```

### Dynamic Operational Grid Growth Rule

- Los grids documentales **siempre crecen** con el número de ítems
- El usuario hace scroll sobre el **documento completo**, nunca dentro de grids pequeños
- Sin `Height`, `MaxHeight`, ni `MinHeight` forzados en secciones documentales

---

## 0b-ext2. Commercial Tax Pattern (ADR-052)

**Una sola fuente de verdad fiscal. NUNCA hardcodear tasas fiscales.**

```
catalogos.TipoImpuesto      = QUÉ impuestos existen + cuál es su tasa
ParametroGlobal (int)       = CUÁL TipoImpuesto usar por default
IConfiguracionFiscalService = resuelve la cadena ParametroGlobal → TipoImpuesto → Tasa
FiscalConstants             = FALLBACK ÚNICAMENTE (si no hay configuración)
```

### Reglas de consumo

```csharp
// ✅ CORRECTO: Tasa desde configuración institucional
_tasaIva = await _configuracionFiscal.ObtenerTasaIvaProductoAsync(ct);
Impuestos = CommercialDocumentCalculator.CalcularImpuestos(lineas, _tasaIva);

// ❌ PROHIBIDO: Tasa hardcodeada
Impuestos = CommercialDocumentCalculator.CalcularImpuestos(lineas, 0.16m);
Impuestos = CommercialDocumentCalculator.CalcularImpuestos(lineas, FiscalConstants.TasaIvaEstandar);

// ✅ CORRECTO: ParametroGlobal fiscal almacena TipoImpuestoId (int)
"impuesto.default.producto" = "3"  // TipoImpuestoId = 3

// ❌ PROHIBIDO: ParametroGlobal fiscal almacena la tasa directamente
"iva.tasa.default" = "0.16"  // Doble fuente de verdad
```

### ParametrosClave

SIEMPRE usar `ParametrosClave.*`. NUNCA strings literales en código.

```csharp
ParametrosClave.Fiscal.ImpuestoDefaultProducto  // "impuesto.default.producto"
ParametrosClave.Fiscal.ImpuestoDefaultServicio   // "impuesto.default.servicio"
ParametrosClave.Fiscal.ImpuestoDefaultCargo      // "impuesto.default.cargo"
```

---

## 0b-ext3. Shared Sequence/Folio Pattern (ADR-051)

**NUNCA generar folios en UI. NUNCA usar ParametroGlobal para consecutivos runtime.**

```csharp
// ✅ CORRECTO: Folio generado en Application layer, atómico
var folio = await _folioGenerator.GenerarFolioAsync(
    empresaId, TipoDocumentoSerie.Cotizacion, sucursalId, ct);
// folio puede ser null si no hay serie configurada — no falla

// ❌ PROHIBIDO: Folio en ViewModel o code-behind
var folio = $"COT-{++_contador}";  // No atómico, no persistido, no institucional
```

**Document Identity Rule:** Cada documento tiene folio propio e independiente.
```
COT-000001 → [convertir] → PED-000001 (NUEVO folio)
```
NUNCA reutilizar el mismo folio en la conversión.

**Concurrencia:** `IFolioGeneratorService` usa `IDbContextFactory` (contexto aislado). Nunca el DbContext scoped del llamador. `.ToListAsync()` antes de `[0]` al usar `SqlQuery<T>` con sentencias DML + OUTPUT.

---

## 0b-ext4. Commercial Charges Pattern (ADR-054)

**Los OtrosCargos NO son productos. NUNCA representarlos con Producto + flag especial.**

```
Servicios (Instalación, Consultoría)  → Producto con TipoProducto.Clave = "SERV"
OtrosCargos (Flete, Maniobras, Seguro) → CotizacionCargo — entidad propia
```

**Fórmula de totales con OtrosCargos:**
```csharp
TotalOtrosCargos = Cargos.Sum(c => c.Importe);
Impuestos = IVA(productos con IvaAplicable=true) + IVA(cargos con AplicaIva=true);
Total = Subtotal + TotalOtrosCargos + Impuestos;
```

**Persistencia de cargos en documento nuevo:**
Los cargos en memoria (IsNuevo=true) se persisten en loop DESPUÉS de `CrearAsync`, antes de `Initialize()`.

---

## 0b-ext5. Global Document Runtime Ownership Pattern (ADR-056)

**Un documento comercial existe como máximo UNA vez en toda la aplicación.**

```csharp
// ✅ CORRECTO: Window key usa DocumentoId del ViewModel (se actualiza tras guardar)
var cotizacionId = ViewModel.DocumentoId?.ToString() ?? _sessionKey;
var windowKey    = $"detached:cotizacion:{cotizacionId}";

// ❌ PROHIBIDO: Window key usa campo readonly del constructor (null para docs nuevos guardados)
var cotizacionId = _cotizacionOriginal?.Id.ToString() ?? _sessionKey;
```

**Actualización de inline tracking tras primer guardado:**
```csharp
// En BtnNueva_Click — obligatorio para Single Document Session Rule cross-host
page.ViewModel.DocumentSaved = () =>
{
    _currentInlineDocumentId = page.ViewModel.DocumentoId;
};
```

---

## 0a. Operational Editable Document Lines Pattern (ADR-041)

**Todo grid de líneas de documento comercial DEBE cumplir este estándar.**

### Anti-patrones PROHIBIDOS

- `DetalleLineaEditable` como POCO plano sin `INotifyPropertyChanged` — la columna Importe no refresca.
- Abrir modal pesado solo para editar cantidad — excesiva fricción operacional.
- Duplicar la fórmula `Cantidad × PrecioUnitario` en múltiples lugares.
- Almacenar `Importe` como campo editable en el modelo — debe ser propiedad calculada.
- Recalcular totales solo en `CollectionChanged` — no detecta mutaciones en líneas existentes.

### Reglas obligatorias

```csharp
// ✅ DetalleLineaEditable DEBE implementar INotifyPropertyChanged
public sealed class DetalleLineaEditable : INotifyPropertyChanged { ... }

// ✅ Importe es siempre propiedad calculada
public decimal Importe => _cantidad * _precioUnitario;

// ✅ Setter de Cantidad notifica Importe y dispara callback
private void SetCantidad(decimal value) {
    _cantidad = value;
    OnPropertyChanged(nameof(Cantidad));
    OnPropertyChanged(nameof(CantidadDouble));
    OnPropertyChanged(nameof(Importe));
    CantidadCambiadaCallback?.Invoke();
}

// ✅ WirarLinea conecta el callback (única vez, en el ViewModel)
private DetalleLineaEditable WirarLinea(DetalleLineaEditable linea) {
    linea.CantidadCambiadaCallback = () => { IsDirty = true; RecalcularTotales(); };
    return linea;
}

// ✅ Entry point único para actualizar cantidad
await ViewModel.ActualizarCantidadAsync(linea, nuevaCantidad);
// negativo → ignorar, 0 → eliminar línea, positivo → actualizar
```

### XAML obligatorio para columna Cantidad

```xml
<!-- ✅ NumberBox inline — NUNCA TextBlock estático para cantidad editable -->
<NumberBox Value="{x:Bind CantidadDouble, Mode=TwoWay}"
           Minimum="0" SpinButtonPlacementMode="Hidden"
           HorizontalContentAlignment="Right"
           Tag="{x:Bind}"
           ValueChanged="NumberBox_Cantidad_ValueChanged"/>

<!-- ✅ Importe y PrecioUnitario con Mode=OneWay para responder a INPC -->
<TextBlock Text="{x:Bind Importe, Mode=OneWay, Converter={StaticResource CurrencyConverter}}"/>
```

### Handler code-behind obligatorio

```csharp
private async void NumberBox_Cantidad_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) {
    if (double.IsNaN(args.NewValue) || args.NewValue == args.OldValue) return;
    if (sender.Tag is not DetalleLineaEditable linea) return;
    // Solo persistir en BD para docs existentes; TwoWay binding ya aplicó en UI
    if (!ViewModel.IsNuevo)
        await ViewModel.ActualizarCantidadAsync(linea, (decimal)args.NewValue);
}
```

---

## 0a-ext4. Single Document Session Rule

**Un documento comercial NO puede existir simultáneamente en múltiples sesiones runtime.**

### Anti-patrones PROHIBIDOS

```csharp
// ❌ PROHIBIDO: abrir documento sin verificar si ya hay sesión activa
private async Task AbrirCotizacionInline(long id)
{
    var result = await _service.ObtenerAsync(id);
    var page = new CotizacionDocumentoPage(result.Value); // ← PUEDE CREAR SEGUNDA INSTANCIA
    ...
}
```

### Patrón obligatorio (en TODA page que abre documentos)

```csharp
private long? _currentInlineDocumentId;  // Tracking de sesión inline

private async Task AbrirDocumentoInline(long id)
{
    // Verificación 1: ¿En ventana detached?
    if (_windowManager.TryActivateWindow($"detached:{tipo}:{id}"))
        return;  // Sesión activa encontrada — NO crear nueva

    // Verificación 2: ¿Ya inline con mismo ID?
    if (ViewModel.IsDocumentSurfaceVisible && _currentInlineDocumentId == id)
        return;

    // Sin sesión activa — crear normalmente
    var result = await _service.ObtenerAsync(id);
    ...
    _currentInlineDocumentId = id;
}

private void OnVolverALista()
{
    _currentInlineDocumentId = null;  // Limpiar tracking
    ...
}
```

### `TryActivateWindow` — contrato

```csharp
// En IWindowManager — Single Document Session Rule
bool TryActivateWindow(string documentKey);
// Busca ventana cuya key interna termina con _{documentKey}
// true → ventana activada, caller debe abortar apertura
// false → no existe, caller puede abrir normalmente

// Clave de convención:
"detached:cotizacion:123"
"detached:pedido:456"
"detached:ot:789"
```

### Aplica a todos los módulos con Document Surface

- Cotizaciones ✅ | Pedidos 🔲 | Ventas 🔲 | OrdenesTrabajo 🔲

---

## 0a-ext3. Commercial Document Workflow Pattern

**Regla crítica**: NUNCA tratar acciones como estados. NUNCA limitar acciones operacionales por estado comercial incorrecto.

### Estados vs Acciones

| Tipo | Ejemplos | Cambia estado |
|---|---|---|
| Estado comercial | Borrador, Aprobada, Convertida, Cancelada | — |
| Acción de workflow | Aprobar, Convertir, Cancelar | SÍ |
| Acción operacional | Enviar, Imprimir, Duplicar | NO |

### Anti-patrones PROHIBIDOS

```csharp
// ❌ PROHIBIDO: acción operacional que cambia estado
private async void BtnEnviar_Click(...)
    => await ViewModel.CambiarEstatusCommand.ExecuteAsync(EstatusCotizacion.Enviada);

// ❌ PROHIBIDO: acción operacional restringida por estado incorrecto
public bool PuedeEnviar => Estatus == EstatusCotizacion.Borrador;

// ❌ PROHIBIDO: estado "Enviada" en flujo normal
EstatusCotizacion.Enviada  // = LEGACY, solo para compatibilidad BD
```

### Patrón correcto

```csharp
// ✅ Acción operacional — sin cambio de estado
private void BtnEnviar_Click(object sender, RoutedEventArgs e)
{
    if (!ViewModel.PuedeEnviar) return;
    ViewModel.SuccessMessage = "Cotización lista para envío.";
    // Future: generar PDF, enviar correo, auditoría
}

// ✅ Guarda correcta — disponible desde cualquier estado activo
public bool PuedeEnviar => !IsNuevo && Estatus is not (Cancelada or Convertida);

// ✅ Acción de workflow — sí cambia estado
private async void BtnAprobar_Click(...) 
    => await ViewModel.CambiarEstatusCommand.ExecuteAsync(Aprobada);
```

### Flujo oficial

```
Borrador → Aprobada → Convertida (terminal)
               ↘ Cancelada (terminal)
```

---

## 0a-ext2. Operational Date Display Pattern

**Todo campo de fecha en documentos comerciales DEBE usar CalendarDatePicker + etiqueta operacional.**

```xaml
<!-- ✅ Patrón institucional obligatorio -->
<StackPanel Spacing="4">
    <TextBlock Text="Fecha" FontWeight="SemiBold" FontSize="12"/>
    <StackPanel Orientation="Horizontal" Spacing="10" VerticalAlignment="Center">
        <CalendarDatePicker x:Name="DpFecha"
                           Date="{x:Bind ViewModel.FechaOffset, Mode=OneWay}"
                           DateChanged="DpFecha_DateChanged"
                           IsEnabled="{x:Bind ViewModel.PuedeEditar, Mode=OneWay}"/>
        <TextBlock Text="{x:Bind ViewModel.FechaOffset, Mode=OneWay,
                         Converter={StaticResource OperationalDateConverter}}"
                   FontSize="13" FontWeight="SemiBold"/>
    </StackPanel>
</StackPanel>
```

```csharp
// Code-behind handler obligatorio
private void DpFecha_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
{
    if (args.NewDate.HasValue)
        ViewModel.FechaOffset = args.NewDate.Value;
}

// Formato: "8 Junio 2026" — via OperationalDateConverter.FormatOperationalDate(date)
```

- **PROHIBIDO**: `DatePicker` con spinner de columnas — clipping, UX incómoda, formato ambiguo
- **PROHIBIDO**: formato numérico ambiguo "06/08/26"
- **OBLIGATORIO**: `OperationalDateConverter` (centralizado en `Converters/`) — no duplicar la lógica de formato

---

## 0a-ext. Selector DTO Hydration Rule

**PROHIBIDO**: crear `DirectorioSelectorDto` manualmente con datos parciales.

```csharp
// ❌ PROHIBIDO — DTO parcial incorrecto
_entidad = new DirectorioSelectorDto
{
    EntityType         = DirectorioEntityType.Empresa, // hardcoded — puede ser Persona
    EmpresaComercialId = relacionComercialId,          // RelacionComercialId ≠ EmpresaComercialId
    DisplayName        = nombreCliente                 // sin email, teléfono, RFC
};

// ✅ CORRECTO — hidratación real desde BD
var dto = await _directorioService.ObtenerDtoParaSelectorAsync(relacionComercialId, ct);
ViewModel.RestaurarEntidadSeleccionada(dto);
SelectorCliente.EntidadSeleccionada = dto;
```

`ObtenerDtoParaSelectorAsync` es el Single Source of Truth. Usarlo en:
- Modo edición (constructor de página)
- Cualquier restauración post-initialize de entidad seleccionada

`RestaurarEntidadSeleccionada` en ViewModel: NO marca `IsDirty` — es restauración, no acción de usuario.

---

## 0b-ext. Global Discount Lifecycle — Uniformity Rule

**El descuento global solo es válido cuando TODAS las líneas tienen el mismo porcentaje.**

### Anti-patrones PROHIBIDOS

- Mantener `DescuentoGlobalPct > 0` cuando las líneas tienen descuentos distintos — es información ambigua.
- Mostrar alerta o confirmación al invalidar el global automáticamente.
- Eliminar descuentos de línea al invalidar el global.

### Regla obligatoria

```csharp
// En NumberBox_Descuento_ValueChanged (code-behind):
// SIEMPRE evaluar si el nuevo valor rompe la uniformidad global
if (nuevoPct != ViewModel.DescuentoGlobalPct)
    ViewModel.InvalidarDescuentoGlobal(); // silencioso, sin dialog

// En ViewModel — método canónico:
internal void InvalidarDescuentoGlobal()
{
    if (DescuentoGlobalPct != 0)
        DescuentoGlobalPct = 0; // Solo el indicador — los descuentos de línea se preservan
}
```

### Guard de no-interferencia

El guard `(decimal)args.NewValue == ViewModel.DescuentoGlobalPct` en `NumberBox_Descuento_ValueChanged` impide que `AplicarDescuentoGlobalALineas` (Phase 1) dispare la invalidación. Solo los cambios MANUALES del usuario llegan a la invalidación.

### Semántica visual obligatoria

| Campo | Label correcto |
|---|---|
| Importe de línea después de descuento | **"Importe Neto"** |
| Suma bruta antes de descuentos | **"Subtotal sin descuentos"** |
| Suma neta después de descuentos | **"Subtotal"** |

---

## 0b. Commercial Discount Pattern (ADR-042)

**Todo descuento comercial en documentos ERP DEBE cumplir este estándar.**

### Anti-patrones PROHIBIDOS

- Fórmula `Cantidad × Precio × (1 − Desc/100)` duplicada fuera de `CommercialDocumentCalculator`.
- Descuentos acumulables (global + línea simultáneos) — ambiguos e incorrectos fiscalmente.
- Aplicar descuento global sin confirmación cuando existen descuentos individuales.
- Calcular IVA sobre el precio bruto (antes de descuento) — IVA va sobre el importe NETO.
- Almacenar `Importe` bruto cuando hay descuento — el `Importe` persistido es siempre el neto.
- Hardcodear porcentajes de descuento en la UI.

### Reglas obligatorias

```csharp
// ✅ CommercialDocumentCalculator es el ÚNICO lugar donde vive la fórmula
decimal importe = CommercialDocumentCalculator.CalcularImporteLinea(cantidad, precio, descuentoPct);

// ✅ DetalleLineaEditable.Importe usa el Calculator (Single Source of Truth)
public decimal Importe
    => CommercialDocumentCalculator.CalcularImporteLinea(_cantidad, _precioUnitario, _descuentoPct);

// ✅ Descuento global NO acumulable — confirmación antes de aplicar
if (pct > 0 && ViewModel.HayDescuentosEnLineas)
{
    var continuar = await MostrarConfirmacionDescuentoGlobalAsync(); // en code-behind
    if (!continuar) return; // usuario canceló
}
await ViewModel.AplicarDescuentoGlobalALineas(pct);

// ✅ DescuentoGlobalPct wrapper double para NumberBox
public double DescuentoGlobalPctDouble => (double)DescuentoGlobalPct;

// ✅ Totales del documento
private void RecalcularTotales()
{
    SubtotalBruto  = Detalles.Sum(d => d.Cantidad * d.PrecioUnitario); // bruto
    Subtotal       = Detalles.Sum(d => d.Importe);                     // neto
    DescuentoTotal = SubtotalBruto - Subtotal;                         // diferencia
    Impuestos      = CommercialDocumentCalculator.CalcularImpuestos(
        Detalles.Select(d => (d.Importe, d.IvaAplicable)),
        FiscalConstants.TasaIvaEstandar);
    Total = CommercialDocumentCalculator.CalcularTotal(Subtotal, Impuestos);
    OnPropertyChanged(nameof(HayDescuento));
}
```

### Persistencia de descuento (por línea)

- `DetalleLineaEditable.DescuentoPct` → se guarda en `CotizacionDetalle.DescuentoPct` (decimal 5,2).
- `Importe` persistido = importe neto (con descuento ya aplicado).
- Descuento global: NO tiene columna propia en `Cotizacion`. Se detecta al cargar si todas las líneas tienen el mismo porcentaje > 0 → `DescuentoGlobalPct` se restaura automáticamente.

### Reutilización en futuros módulos (Pedidos, OC, Ventas, Facturación)

- Usar siempre `CommercialDocumentCalculator` — jamás reimplementar la fórmula.
- Agregar `DescuentoPct` a `PedidoDetalle`, `OrdenCompraDetalle`, `VentaDetalle` siguiendo el mismo patrón.
- El script SQL de referencia está en `Documentation/Scripts/AddDescuentoPct_CotizacionDetalle.sql`.

---

## 0. Operational Commercial Document Pattern (ADR-040)

**Todo documento comercial ERP (Cotización, Pedido, etc.) DEBE cumplir este estándar mínimo.**

### Anti-patrones PROHIBIDOS

- Líneas duplicadas del mismo producto — siempre merge (sumar cantidad).
- Auto-save silencioso al cerrar — la decisión es del usuario.
- Perder cliente visual o runtime state en cualquier modo.
- Cálculos de totales en múltiples lugares — centralizar en RecalcularTotales().
- IVA hardcodeado (0.16) — usar `FiscalConstants.TasaIvaEstandar`.
- Lógica fiscal en UI/code-behind.

### Reglas obligatorias

```csharp
// ✅ Merge-or-add siempre (no líneas duplicadas)
await ViewModel.AgregarOIncrementarDetalleAsync(detalle);

// ✅ Constante fiscal centralizada
using Ybridio.Domain.Common;
decimal impuestos = basIva * FiscalConstants.TasaIvaEstandar;

// ✅ Fórmula estándar de totales
// Subtotal   = SUM(Cantidad × PrecioUnitario)
// Impuestos  = SUM(líneas con IvaAplicable) × TasaIvaEstandar
// Total      = Subtotal + Impuestos

// ✅ IsDirty se marca en TODA mutación
IsDirty = true;  // al agregar/eliminar línea, cambiar cliente, cambiar observaciones

// ✅ IsDirty se resetea SOLO tras guardar exitosamente
IsDirty = false;

// ✅ Confirmación antes de cerrar cuando IsDirty
if (!await MostrarConfirmacionCierreAsync()) return;
```

### Estructura de métodos obligatoria para documentos comerciales

- `ObtenerLineaExistente(productoId)` — busca línea existente del mismo producto
- `AgregarOIncrementarDetalleAsync(detalle)` — entry point principal: merge-or-add
- `IncrementarCantidadAsync(linea, incremento)` — suma cantidad, persiste si es doc existente
- `RecalcularTotales()` — único punto de cálculo; llama a `CalcularSubtotal()` + `CalcularImpuestos()`
- `CalcularSubtotal()` — `SUM(Detalles.Importe)`
- `CalcularImpuestos()` — `SUM(líneas IVA) × FiscalConstants.TasaIvaEstandar`
- `MostrarConfirmacionCierreAsync()` — diálogo: Guardar / No Guardar / Cancelar

### Visibilidad de cliente

- El cliente seleccionado DEBE mostrarse en TODOS los modos: nuevo, edición, inline, standalone.
- Chip/selector DEBE restaurarse en rehost (ADR-039).

### Formato monetario

- Usar siempre `DecimalToCurrencyConverter` en XAML para valores financieros.
- NUNCA mostrar decimales sin formato tipo `3337.000000000`.

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
- WindowManager (ADR-029: single source of truth window lifecycle)
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

### Security Runtime Concurrency (ADR-026)

**Patrón aplicado**: `PermisoService` usa **single-flight guard** con `SemaphoreSlim` global para serializar evaluaciones de permisos runtime y evitar DbContext concurrency exceptions durante navegación, Document Surface activation, y pre-checks simultáneos.

```csharp
// ✅ CORRECTO — PermisoService.TienePermisoAsync
private static readonly SemaphoreSlim _authSemaphore = new(1, 1);

public async Task<bool> TienePermisoAsync(
    Guid usuarioId, string clave, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(clave))
        return false;

    await _authSemaphore.WaitAsync(ct);  // ← Single-flight: serializar evaluaciones
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
```

**Por qué necesario**:
- Navegación rápida entre módulos (Clientes ↔ Cotizaciones ↔ Pedidos)
- Activación/desactivación Document Surfaces
- Pre-checks autorización en `OnNavigatedTo` + bindings + comandos async
- Runtime Diagnostic Panel refresh + navegación usuario
- Múltiples tabs Workspace con evaluaciones concurrentes

**Resultado**: SIN `InvalidOperationException` DbContext, autorización consistente, navegación estable.

**Anti-patterns prohibidos**:
```csharp
// ❌ NO aislar DbContext con Task.Run
Task.Run(() => await _permisos.TienePermisoAsync(...));

// ❌ NO cambiar DbContext a singleton
services.AddSingleton<ErpDbContext>();  // PROHIBIDO

// ❌ NO capturar DbContext en campos static
private static ErpDbContext _ctx;  // PROHIBIDO
```

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

### UX Button Styles Standard — Regla Anti-Proliferación

Los estilos de botón definidos en `App.xaml` deben tener **nombres semánticos orientados a contexto UX**, no nombres técnicos/genéricos.

#### Regla principal

**❌ PROHIBIDO** — nombres técnicos/genéricos que no portan contexto:
```xml
<!-- No describe dónde ni por qué se usa. Cualquier botón transparente "califica". -->
<Style x:Key="TransparentButtonStyle" TargetType="Button"/>
<Style x:Key="GhostButtonStyle" TargetType="Button"/>
<Style x:Key="FlatButtonStyle" TargetType="Button"/>
```

**✅ OBLIGATORIO** — nombres semánticos que reflejan contexto UX real:
```xml
<!-- Claro: solo para acción de retroceso en Document Surface headers. -->
<Style x:Key="DocumentSurfaceBackActionButtonStyle" TargetType="Button"/>
```

#### Estilos de botón definidos (catálogo oficial)

| Estilo | Contexto de uso | Dónde NO usar |
|---|---|---|
| `DocumentSurfaceBackActionButtonStyle` | Botón `←` volver en Document Surface headers (ADR-031). Fondo transparente, sin borde, padding 6px, CornerRadius 4. | Fuera de Document Surface headers. Botones primarios. Acciones de guardado. |
| `AccentButtonStyle` *(WinUI built-in)* | Acción primaria de un formulario (Guardar, Confirmar). | Acciones secundarias o destructivas. |

#### Regla de creación de nuevos estilos

Antes de crear un nuevo estilo de botón:
1. **¿Existe ya uno que aplique semánticamente?** → reutilizarlo.
2. **¿El contexto es diferente?** → crear uno nuevo con nombre semántico (NO genérico).
3. **Documentar** el nuevo estilo en este catálogo y con comentario XML en `App.xaml`.
4. **Nunca** crear un estilo genérico reutilizable "para cualquier botón transparente" — eso es proliferación garantizada.

---

## 11. Grid Standards

Todos los grids deben usar: buscador, contador de registros, virtualización, filtros estándar, diseño consistente.

**Filtros temporales estándar**: Hoy · 7 días · 30 días · 90 días · 6 meses · 1 año · Todo.

**Contenedor estándar**:
```xaml
<Border Margin="20,8,20,0" Background="White" BorderBrush="#E5E5E5" BorderThickness="1">
```

---

## 11b. Visual Design System (ADR-033) — OBLIGATORIO

### Styles/ = Source of Truth Visual

`Styles/` es la **fuente de verdad visual oficial** del ERP. Todo estilo reutilizable vive ahí.

**`App.xaml` es SOLO bootstrap** — merges + `XamlControlsResources`. NUNCA contiene estilos.

### Estructura oficial

```
Styles/
  Styles.xaml                       ← Dictionary maestro (único punto de entrada)
  Layout/LayoutBase.xaml            ← Spacing tokens + content boundary + containers (ADR-034)
  CommandBars/CommandBarsBase.xaml  ← CommandBar styles operacionales (ADR-034)
  Buttons/ButtonsBase.xaml          ← Botones e interacciones
  DataGrid/DataGridBase.xaml        ← Listas, grids, tablas
  Forms/FormBase.xaml               ← Formularios CRUD
  Tabs/TabsBase.xaml                ← Navegación por tabs (Module + Workspace layers)
```

### Reglas obligatorias para IA y desarrolladores

#### 1. NUNCA agregar estilos en App.xaml

```xaml
<!-- ❌ PROHIBIDO -->
<!-- App.xaml -->
<Style x:Key="CualquierEstilo" TargetType="Button">...</Style>

<!-- ✅ CORRECTO -->
<!-- Styles/Buttons/ButtonsBase.xaml -->
<Style x:Key="MiEstiloSemántico" TargetType="Button">...</Style>
```

#### 2. Naming semántico obligatorio

El nombre del estilo debe expresar **intención UX**, no apariencia visual.

```
❌ PROHIBIDO              ✅ CORRECTO
TransparentButtonStyle  → DocumentSurfaceBackActionButtonStyle
GrayButtonStyle         → ModuleSecondaryActionButtonStyle
BlueCard                → WorkspaceActiveDocumentCardStyle
MainGridStyle           → ModuleListViewContainerStyle
GenericTab              → OutlookTabItemStyle / WorkspaceTabItemStyle
```

#### 3. PROHIBIDO estilos inline

```xaml
<!-- ❌ PROHIBIDO — estilos inline como solución rápida -->
<Button>
    <Button.Style>
        <Style TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
        </Style>
    </Button.Style>
</Button>

<!-- ✅ CORRECTO — usar StaticResource del Design System -->
<Button Style="{StaticResource DocumentSurfaceBackActionButtonStyle}"/>
```

#### 4. Antes de crear un estilo nuevo

1. Buscar en `Styles/` si ya existe uno compatible.
2. Si existe, reusar. PROHIBIDO duplicar estilos visualmente similares con nombres distintos.
3. Si no existe, crear en el subdirectorio semántico correcto.
4. Registrar en `Styles/Styles.xaml` si es un nuevo subdominio.

#### 5. Agregar un nuevo dominio visual

```
1. Crear  Styles/<Dominio>/<Dominio>Base.xaml
2. Agregar en Styles/Styles.xaml:
   <ResourceDictionary Source="ms-appx:///Styles/<Dominio>/<Dominio>Base.xaml"/>
3. Registrar en .csproj como <Page Update="Styles\<Dominio>\<Dominio>Base.xaml"> con MSBuild:Compile
4. Documentar en Documentation/CLAUDE_RULES.md §Design System
5. Registrar en Documentation/DECISIONS.md como ADR
```

### Subdominios actuales y su responsabilidad

| Archivo | Responsabilidad | Estilos / Tokens actuales |
|---|---|---|
| `Layout/LayoutBase.xaml` | Spacing tokens + content boundary + containers | `ErpSpace*`, `ErpContentBoundary*`, `ErpCommandBarPadding`, `ErpOperationalCardStyle`, `ErpTotalesCardStyle` |
| `CommandBars/CommandBarsBase.xaml` | CommandBar operacionales | `ErpModuleCommandBarStyle`, `ErpDocumentCommandBarStyle` |
| `Buttons/ButtonsBase.xaml` | Botones e interacciones | `DocumentSurfaceBackActionButtonStyle` |
| `DataGrid/DataGridBase.xaml` | Listas, grids, tablas | `ErpListViewItemStyle`, constantes `ErpRowHeight`, etc. |
| `Forms/FormBase.xaml` | Formularios CRUD | `ErpFormTitleStyle`, constantes `ErpFormFieldSpacing`, etc. |
| `Tabs/TabsBase.xaml` | Tabs de navegación | `OutlookTabItemStyle`, `WorkspaceTabItemStyle` |

### Anti-patterns PROHIBIDOS

```
❌ Estilos nuevos en App.xaml
❌ Nombres ambiguos (describe apariencia, no contexto UX)
❌ Estilos inline (<Button.Style>...)
❌ Copiar/pegar estilos entre archivos
❌ Duplicar visual states con nombres distintos
❌ Design System improvisado sin ADR
❌ Estilos huérfanos (definidos pero nunca usados)
```

---

## 11c. Pixel Perfect Operational Layout System (ADR-034) — OBLIGATORIO

### Regla fundamental

**PROHIBIDO** márgenes, padding o spacing arbitrarios. Todo spacing proviene de tokens del Layout System.

### Spacing scale oficial

```xaml
<!-- ✅ CORRECTO — usar tokens -->
<StackPanel Spacing="{StaticResource ErpSpace8}"/>
<Border Margin="{StaticResource ErpContentBoundaryThickness}"/>

<!-- ❌ PROHIBIDO — valores hardcoded arbitrarios -->
<StackPanel Spacing="7"/>
<Border Margin="13,0,13,0"/>
```

### Content boundary rule

Todos los elementos operacionales (CommandBar, grids, forms, status, totales) comparten la **misma boundary horizontal de 20px**.

```xaml
<!-- ✅ CORRECTO — CommandBar alineada con contenido -->
<CommandBar Style="{StaticResource ErpModuleCommandBarStyle}"/>
<Border Style="{StaticResource ErpOperationalCardStyle}">...</Border>

<!-- ❌ PROHIBIDO — CommandBar desalineada -->
<CommandBar HorizontalAlignment="Left" Margin="8,0,0,0"/>
```

### Document Surface visibility rule

Cuando `IsDocumentSurfaceVisible=true`, la status bar del listado **debe ocultarse**:

```xaml
<!-- ✅ CORRECTO -->
<Border Style="{StaticResource ErpGridStatusBarStyle}"
        Margin="{StaticResource ErpStatusRegionMargin}"
        Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay,
                     Converter={StaticResource InverseBoolToVisibilityConverter}}"/>
```

### Tokens de referencia rápida

| Token | Valor | Uso |
|---|---|---|
| `ErpContentBoundaryThickness` | `20,0,20,0` | Margin lateral estándar |
| `ErpContentBoundaryWithTopGap` | `20,8,20,0` | Cards/grids con gap superior |
| `ErpContentBoundaryBottom` | `20,0,20,16` | Último elemento de página |
| `ErpCommandBarPadding` | `6,0,0,0` | Alinea CommandBar con 20px boundary |
| `ErpFilterRegionPadding` | `20,8,20,4` | Row de búsqueda/filtros |
| `ErpStatusRegionMargin` | `20,0,20,8` | Status bar |
| `ErpDocumentHeaderPadding` | `20,8,20,8` | Header strip de Document Surface |
| `ErpDocumentFormPadding` | `16,12,16,12` | Padding interno de form cards |

---

## 11d. Operational Column Density System (ADR-035) — OBLIGATORIO

### Regla fundamental

**PROHIBIDO** anchos de columna arbitrarios (`Width="150"`, `Width="200"`).
La distribución de columnas surge del sistema operacional institucional.

### Tipos de columna oficiales

| Tipo | Ancho XAML | Token | Ejemplos |
|---|---|---|---|
| **PRIMARY EXPANDABLE** | `Width="*"` o `Width="2*"` | — (star, inline) | Cliente, Nombre, Producto, Descripción |
| **COMPACT SEMANTIC** | token fijo | `OgColCompact` (90), `OgColDate` (100), `OgColStatus` (110) | Folio, Fecha, Estado, Tipo |
| **FINANCIAL COMPACT** | token fijo | `OgColFinancial` (130) | Total, Precio, Costo, LimiteCredito |
| **GUTTER** | token fijo | `OgColGutter` (8) | Primera columna siempre |

### Fill strategy obligatoria

La columna principal operacional SIEMPRE debe ocupar el espacio sobrante útil con `Width="*"`.

```xaml
<!-- ✅ CORRECTO — Cliente expandible, sin desierto visual central -->
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="{StaticResource OgColGutter}"/>    <!-- gutter -->
    <ColumnDefinition Width="{StaticResource OgColCompact}"/>   <!-- Folio -->
    <ColumnDefinition Width="*"/>                               <!-- Cliente PRIMARY EXPANDABLE -->
    <ColumnDefinition Width="{StaticResource OgColDate}"/>      <!-- Fecha -->
    <ColumnDefinition Width="{StaticResource OgColStatus}"/>    <!-- Estado -->
    <ColumnDefinition Width="{StaticResource OgColFinancial}"/> <!-- Total -->
</Grid.ColumnDefinitions>

<!-- ❌ PROHIBIDO — anchos arbitrarios, columna principal no expandible -->
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="8"/>
    <ColumnDefinition Width="70"/>
    <ColumnDefinition Width="3*"/>   <!-- "3*" no elimina espacio muerto si hay otras grandes -->
    <ColumnDefinition Width="100"/>
    <ColumnDefinition Width="150"/>  <!-- arbitrario -->
    <ColumnDefinition Width="200"/>  <!-- arbitrario -->
</Grid.ColumnDefinitions>
```

### El header SIEMPRE debe ser idéntico al DataTemplate

Las definiciones de columnas del `<Border OgHeaderContainerStyle>` y las del `<DataTemplate>` deben ser **exactamente iguales** para mantener alineación visual perfecta.

---

## 11e. Financial Formatting Semantics (ADR-035) — OBLIGATORIO

### Regla fundamental

**TODO** valor financiero visible en el UI debe incluir símbolo monetario.

```
✅ CORRECTO: $3,337.00  $347,746.00  $0.00
❌ PROHIBIDO: 3337.00   347746.00    0
```

### Cómo formatear — SIEMPRE converter, NUNCA ViewModel

```xaml
<!-- ✅ CORRECTO — usar DecimalToCurrencyConverter en el binding -->
<TextBlock Text="{x:Bind Total, Converter={StaticResource CurrencyConverter}, Mode=OneTime}"
           Style="{StaticResource OgCurrencyTextStyle}"/>

<!-- ❌ PROHIBIDO — formatear en el ViewModel -->
<!-- ViewModel: public string TotalFormateado => Total.ToString("C"); -->

<!-- ❌ PROHIBIDO — mostrar decimal crudo sin converter -->
<TextBlock Text="{x:Bind Total}"/>
```

### Declarar converter en Page.Resources

```xaml
<Page.Resources>
    <converters:DecimalToCurrencyConverter x:Key="CurrencyConverter"/>
</Page.Resources>
```

### Estilo obligatorio para valores financieros

```xaml
Style="{StaticResource OgCurrencyTextStyle}"
<!-- o equivalente: -->
Style="{StaticResource OgCellFinancialStyle}"
```

Ambos aplican: `FontWeight=SemiBold`, `TextAlignment=Right`, `FontSize=13`.

### Anti-patterns PROHIBIDOS

```
❌ Mostrar decimales sin símbolo monetario ($)
❌ Formatear en ViewModel o code-behind
❌ Alinear valores financieros a la izquierda
❌ Usar FontWeight=Normal en columnas monetarias
❌ Columnas financieras sin OgColFinancial (130px)
```

---

## 11f. Business Partner Model (ADR-036) — OBLIGATORIO

### Entidades del dominio (Ybridio.Domain.Catalogos)

| Entidad | Schema BD | Propósito |
|---|---|---|
| `Persona` | `core.Persona` | Persona física / contacto del directorio |
| `EmpresaComercial` | `core.EmpresaComercial` | Empresa externa (no tenant) con RFC |
| `RelacionComercial` | `core.RelacionComercial` | Rol comercial del socio frente al tenant |
| `TipoRelacionComercial` | enum | `Prospecto`, `Cliente`, `Proveedor`, `Mixto` |

### Reglas obligatorias

- Todos los documentos de venta (`Cotizacion`, `Pedido`, `OrdenTrabajo`, `Venta`, `Factura`) usan **`RelacionComercialId`** como FK. Nunca `ClienteId`.
- `NombreCliente` se mantiene como campo **denormalizado** para integridad histórica de documentos.
- El selector de socios en documentos usa **`RelacionComercialSelectorDto`** y **`IRelacionComercialService.ListarParaSelectorAsync`**.
- `RelacionComercial` tiene FK exclusiva a `Persona` XOR `EmpresaComercial` — nunca ambas a la vez.
- Los nuevos permisos son **`Directorio.Ver`** y **`Directorio.Editar`** (`PermisosClave.Directorio`).
- El módulo shell se llama **`"Directorio"`** (no `"Contactos"`).

### DTOs del selector (WinUI)

```csharp
// En Ybridio.Application.DTOs.Directorio
public sealed class RelacionComercialSelectorDto
{
    public int    Id                { get; init; }
    public string NombreSocio       { get; init; } = string.Empty;  // NombreCompleto o RazonSocial
    public string TipoSocio         { get; init; } = string.Empty;  // "Persona Física" o "Empresa"
    public string TipoRelacionDisplay { get; init; } = string.Empty; // "Cliente", "Proveedor", etc.
}
```

### XAML — DataTemplate del AutoSuggestBox

```xaml
xmlns:dir="using:Ybridio.Application.DTOs.Directorio"
...
<DataTemplate x:DataType="dir:RelacionComercialSelectorDto">
    <!-- usar NombreSocio, TipoSocio, TipoRelacionDisplay -->
</DataTemplate>
```

### Anti-patterns PROHIBIDOS

```
❌ Usar ClienteId en documentos de venta nuevos
❌ Usar IClienteService en nuevos ViewModels
❌ Crear RelacionComercial con PersonaId Y EmpresaComercialId simultáneamente
❌ Exponer entidades de dominio directamente a la UI (pasar por DTOs)
❌ Lógica de negocio de socios comerciales en WinUI
```

---

## 12. Command Bars

Las command bars deben: ser context-aware, reutilizar el estándar ERP, mantener agrupación consistente.

**No crear ribbons complejos innecesarios.**

---

## 13. Window Management Standards — Centralized Runtime Authority (ADR-029)

**`WindowManager` es single source of truth OBLIGATORIO para TODO window lifecycle management.**

### Regla crítica: NO window management manual

```csharp
// ❌ PROHIBIDO: new Window() fuera de WindowManager
var window = new Window { Title = "Detalle" };
var hwnd = WindowNative.GetWindowHandle(window);
var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
appWindow.Resize(new SizeInt32(900, 700));
window.Activate();

// ✅ CORRECTO: usar WindowManager centralizado
_windowManager.OpenWindow<ProductoDetailWindow, int>(
    productoId,
    () => new ProductoDetailWindow(producto),
    new WindowOptions { Width = 900, Height = 700 });
```

### Anti-patterns explícitos

**PROHIBIDO**:
- `new Window()` fuera de factories pasadas a `WindowManager.OpenWindow(...)`
- `AppWindow` manual disperso en Pages/ViewModels
- Lifecycle handlers duplicados (`window.Closed += ...` fuera de manager)
- Tracking paralelo de ventanas (`_ventanasAbiertasPorMi` counters locales)
- Services window management secundarios (`IDetachedWindowManager`, `IDialogManager`, etc.)
- Window ownership Win32 manual fuera del manager
- Policy enforcement fragmentado (validaciones límite en ViewModels)

### Detached Windows Policy (ADR-028 + ADR-029)

**Límite máximo global**: 2 ventanas detached activas simultáneamente.

**Convention**: Keys con prefix `"detached:"` activan policy límite automáticamente.

```csharp
// Detached window key convention
var detachedKey = $"detached:cotizacion:{cotizacionId}";

try
{
    _windowManager.OpenWindow<DetachedDocumentWindow, string>(
        key: detachedKey,
        factory: () => new DetachedDocumentWindow(documentPage, titulo),
        options: new WindowOptions { Width = 1200, Height = 800 });
}
catch (DetachedWindowLimitException ex)
{
    // Mostrar ContentDialog operacional al usuario
    await MostrarMensajeLimiteVentanasAsync(ex);
}
```

### Lifecycle centralizado

`WindowManager` maneja automáticamente:
- ✅ Creación vía factory
- ✅ Ownership Win32 para z-order garantizado
- ✅ Tamaño y posicionamiento (CenterOwner, CenterScreen, Cascade)
- ✅ Activación y focus (BringToFront multi-layer)
- ✅ Reutilización instancias existentes (NO duplicar)
- ✅ Tracking centralizado (`_windows` dictionary único)
- ✅ Cleanup automático (`window.Closed` handler)
- ✅ Policy enforcement global (ej: máximo 2 detached)
- ✅ Logging diagnóstico `[WindowManager]`

**Pages/ViewModels contienen SOLO**: una línea lógica `_windowManager.OpenWindow(...)`.

**NO dispersar**: creación manual, ownership, resize, activate, closed handlers, tracking.

### Extensión futura

Nuevas policies (ej: máximo 3 dialogs, solo 1 wizard) se agregan **EXCLUSIVAMENTE** en `WindowManager`.

**Convention key prefixes**:
- `detached:` → máximo 2 simultáneas (ADR-028)
- `dialog:` → (futuro) máximo 3 simultáneas
- `wizard:` → (futuro) solo 1 activo
- `detail:` → sin límite

**Documentación completa**: Ver `Documentation/ADR-029-Window-Management-Standards.md`

---

## 14. SQL Server — Sin Migraciones EF

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

## 15. Naming Standards

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
- UI Services (Session, Workspace, **WindowManager**) → `Singleton`
- `IPermissionCache` → `Singleton`

---

## 16. Performance

**No degradar**: grids, EF queries, navegación, observabilidad, runtime.

**Reutilizar**: `MemoryPermissionCache` (TTL 10 min), resolvers y servicios existentes.

**Patrón de carga lazy** en TabViews: flags booleanos + `LoadTab()` + `ILiveContextReporter`. No recargar datos al re-seleccionar un tab ya cargado.

---

## 17. MiniERP PYME Philosophy

Este ERP está orientado a **PYMES**.

**No convertirlo en**: SAP, Oracle, ERP financiero corporativo.

**Mantener**: simplicidad, rapidez, claridad operacional.

---

## 17b. Regla de Dominio — RelacionComercial es Entidad Transaccional (ADR-038)

**REGLA OBLIGATORIA (no negociable)**: `RelacionComercial` es un vínculo comercial operativo/transaccional. NO es un catálogo maestro de UI.

### Source of truth del Directorio

| Entidad | Rol |
|---|---|
| `core.Persona` | Source of truth para personas físicas / contactos |
| `core.EmpresaComercial` | Source of truth para empresas externas / personas morales |
| `core.RelacionComercial` | Vínculo operativo — solo existe cuando hay transacción real |

### GetOrCreate Pattern — obligatorio para todo flujo comercial

```csharp
// ✅ CORRECTO — al guardar cotización/pedido/venta/OT
var rc = await _relacionComercialService.GetOrCreateAsync(
    _session.EmpresaId, _entidadDirectorioSeleccionada!, _session.Usuario.Id, ct);
RelacionComercialId = rc.Value;
```

### Selector institucional — busca el Directorio directamente

```csharp
// ✅ CORRECTO — IDirectorioService + DirectorioSelectorDto
var resultados = await _directorioService.BuscarParaSelectorAsync(empresaId, termino, ct);

// ❌ PROHIBIDO — selector que busca RelacionComercial
var resultados = await _relacionComercialService.ListarParaSelectorAsync(empresaId, termino, ct);
```

### Anti-patterns PROHIBIDOS (ADR-038)

- ❌ Usar `RelacionComercial` como catálogo de búsqueda para selectores UI.
- ❌ Normalización masiva preventiva que genere `RelacionComercial` vacías.
- ❌ Exigir existencia previa de `RelacionComercial` para que una entidad aparezca en el selector.
- ❌ Scripts `.sql` que creen `RelacionComercial` en bulk sin transacción real de por medio.
- ❌ Sincronización artificial Directorio ↔ `RelacionComercial`.
- ❌ Crear `RelacionComercial` fantasma solo para alimentar UI.

### Flujo esperado

1. Usuario busca en selector → `IDirectorioService` → retorna `Persona` o `EmpresaComercial`.
2. Usuario selecciona → ViewModel guarda `DirectorioSelectorDto?`.
3. Al guardar documento → `GetOrCreateAsync()` → reutiliza o crea `RelacionComercial`.
4. Documento se persiste con `RelacionComercialId` ya resuelto.

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

## 12. Document Surface UX Pattern (§ADR-032 — Patrón Institucional Oficial)

### Objetivo

Apertura de documentos CRUD/documentales con dos modos exclusivos y claramente diferenciados. Sin estados ambiguos.

### Principio

**INLINE contextual** = el documento reemplaza el grid dentro del módulo. El usuario permanece en contexto. Rápido y operacional.  
**WINDOW standalone** = ventana OS real independiente (via `IWindowManager`). Multitarea real. El módulo regresa al grid.

**NO existe un tercer modo.** Split view, detachable, y hybrid fueron eliminados (ADR-032).

---

### Reglas Oficiales UX (ADR-032)

#### 1. Layout: Content Replacement (INLINE — único modo contextual)

**ÚNICO modo contextual** — cuando el surface está activo, el grid se oculta completamente:

```xaml
<Grid Grid.Row="2">
    <!-- Listado — visible cuando NO hay surface activo -->
    <Border Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay,
                                  Converter={StaticResource InverseBoolToVisibilityConverter}}">
        <ListView ItemsSource="{x:Bind ViewModel.Items, Mode=OneWay}" ... />
    </Border>
    <!-- Document Surface INLINE — reemplaza el grid -->
    <ContentPresenter Content="{x:Bind ViewModel.DocumentSurfaceContent, Mode=OneWay}"
                      Visibility="{x:Bind ViewModel.IsDocumentSurfaceVisible, Mode=OneWay}"/>
</Grid>
```

#### 2. Controles del documento según modo

**Modo INLINE** (`EsInlineMode = true`): mostrar:
- `BtnVolverALista` — "← Volver a Lista" (cierra el surface)
- `BtnAbrirEnVentana` en `SecondaryCommands` — "Abrir en nueva ventana"

**Modo WINDOW standalone** (`EsInlineMode = false` / default): ocultar ambos. La ventana es standalone y no pertenece al módulo.

Implementación estándar:

```csharp
public bool EsInlineMode
{
    get => _esInlineMode;
    set
    {
        _esInlineMode = value;
        var vis = value ? Visibility.Visible : Visibility.Collapsed;
        BtnVolverALista.Visibility   = vis;
        BtnAbrirEnVentana.Visibility = vis;
    }
}
private bool _esInlineMode;
```

#### 3. Window Mode — apertura de ventana OS real

Al pulsar "Abrir en nueva ventana":
1. Abrir ventana real usando `IWindowManager.OpenWindow<DetachedDocumentWindow, string>(key: "detached:...", ...)`.
2. Invocar `VolverALista?.Invoke()` para cerrar el inline surface y devolver el módulo al grid.
3. La nueva página se crea sin `EsInlineMode = true` (controles inline ocultos por defecto).

```csharp
private void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
{
    _windowManager.OpenWindow<DetachedDocumentWindow, string>(
        key: $"detached:modulo:{id}",
        factory: () => new DetachedDocumentWindow(new DocumentoPage(dto), titulo),
        options: new WindowOptions { Width = 1200, Height = 800 });
    VolverALista?.Invoke(); // cierra inline automáticamente
}
```

#### 4. Wiring en el módulo host

```csharp
private void AbrirInline(MiDto dto)
{
    var page = new MiDocumentoPage(dto);
    page.VolverALista = async () => await ViewModel.CerrarDocumentSurfaceAsync();
    page.EsInlineMode = true; // activa controles inline
    ViewModel.DocumentSurfaceContent   = page;
    ViewModel.IsDocumentSurfaceVisible = true;
}
```

#### 5. ViewModel del módulo (mínimo requerido)

```csharp
[ObservableProperty] private bool    isDocumentSurfaceVisible;
[ObservableProperty] private object? documentSurfaceContent;

public async Task CerrarDocumentSurfaceAsync()
{
    IsDocumentSurfaceVisible = false;
    DocumentSurfaceContent   = null;
    await RefrescarAsync();
}
```

**PROHIBIDO agregar**: `IsDocumentSurfaceDetached`, `ToggleDetach`, split column definitions, layout mutation code.

---

### Anti-Patterns PROHIBIDOS (ADR-032)

```csharp
// ❌ PROHIBIDO — split view / detachable mode
IsDocumentSurfaceDetached = true;
AjustarLayoutDetached(isDetached);
ToggleDetach?.Invoke();

// ❌ PROHIBIDO — columnas split en XAML
<ColumnDefinition x:Name="SplitterColumn" Width="4"/>
<ColumnDefinition x:Name="SurfaceColumn" Width="3*"/>

// ❌ PROHIBIDO — menú contextual vacío o decorativo
<AppBarButton Label="Desacoplar Surface" .../>  // sin utilidad real

// ❌ PROHIBIDO — ventanas sin WindowManager
new Window { Content = page };

// ❌ PROHIBIDO — mostrar BtnVolverALista en ventana standalone
page.EsInlineMode = false; // default correcto; NO llamar para standalone
```

---

### Implementación de referencia: Cotizaciones (módulo piloto ADR-032)

- `CotizacionesViewModel` — solo `IsDocumentSurfaceVisible` + `DocumentSurfaceContent` + `CerrarDocumentSurfaceAsync`.
- `CotizacionesPage.xaml` — grid de content replacement, sin split columns.
- `CotizacionesPage.xaml.cs` — wiring limpio con `EsInlineMode = true`.
- `CotizacionDocumentoPage.xaml` — `BtnVolverALista` y `BtnAbrirEnVentana` con `Visibility="Collapsed"` default.
- `CotizacionDocumentoPage.xaml.cs` — `EsInlineMode` setter + `BtnAbrirEnVentana_Click` abre ventana y cierra inline.

Este es el patrón a replicar para Clientes, Productos, Pedidos, Ventas, Órdenes de Trabajo y demás módulos CRUD/documentales.




**CUÁNDO USAR Detachable Mode**:
- ~~ELIMINADO~~: Detachable Mode fue deprecado en ADR-032. Ver §12 para el patrón actual.

#### 2. Transiciones

**NO** implementar animaciones complejas.

**USAR**:
- Transición instantánea o muy sutil
- Cambio directo de visibilidad mediante binding

#### 3. Comportamiento Guardar

**Después de Guardar**:
1. Refrescar automáticamente el grid de listado
2. Cerrar el Document Surface
3. Volver al listado

**Flujo típico PYME**: `crear → guardar → seguir trabajando en lista`

#### 4. Navegación "← Volver a Lista"

- Ubicación: primer botón en CommandBar del Document Surface (solo en modo INLINE)
- Icon: `&#xE72B;` (Back)
- Acción: cerrar surface sin guardar, volver al grid

#### 5. Workflows Complejos

**Workflows complejos permanecen usando Workspace Tabs persistentes**:
- OT complejas (diseño → producción → QA)
- Multi-documento (Venta ↔ Pedido ↔ OT)
- Comparación/análisis

---

### Validación UX Obligatoria (ADR-032)

- ✅ Grid → doble clic → INLINE correcto
- ✅ INLINE → Volver a lista correcto
- ✅ INLINE → Abrir nueva ventana correcto (cierra inline automáticamente)
- ✅ Ventana standalone: SIN volver a lista, SIN menú contextual inline
- ✅ Multi-window estable (límite 2 — ADR-028/029)
- ✅ Sin split layouts, sin overlaps visuales
- ✅ Runtime Observability funcional
- ✅ WorkspaceService intacto

---

## 12b. Document Surface Visual Separation Standard (ADR-031) — OBLIGATORIO

### Jerarquía UX oficial

```
Tabs módulo (navegación)
    ↓
Document Surface Header (documento activo — operacional)
    ↓
Toolbar operacional (CommandBar documento)
    ↓
Contenido formulario/grid
```

**NUNCA** mezclar niveles visuales.

### Regla: CRUDs simples → Inline Document Surface

Los siguientes casos **NO deben** abrir tabs de workspace:
- Nueva/editar Pedido
- Nueva/editar OT
- Nueva/editar Venta
- Cualquier CRUD simple de módulo

**Usar siempre**: `IsDocumentSurfaceVisible` + `DocumentSurfaceContent` + `ContentPresenter` inline.

**PROHIBIDO** para CRUDs simples:
```csharp
// ❌ ANTI-PATTERN: genera tab documental ensimado
_workspace.OpenTab(key: "pedido-nuevo-...", ...);
_workspace.OpenOrActivateDocumentTabAsync(key: "pedido-123", ...);
```

**CORRECTO** para CRUDs simples:
```csharp
// ✅ PATTERN: inline Document Surface
var page = new PedidoDocumentoPage(null);
page.OnCerrar = async () => await ViewModel.CerrarDocumentSurfaceAsync();
ViewModel.AbrirDocumento(page);
```

`IWorkspaceService` queda reservado para: navegación cruzada entre documentos relacionados, workflows multi-paso complejos, análisis persistente.

### Anti-patterns PROHIBIDOS (Document Surface)

- Línea azul estilo tab en documento activo
- Close × estilo tab en documento activo
- Transparencia/overlay sobre tabs de módulo
- Header tipo browser (Chrome/Edge look)
- Tabs documentales bajo tabs de módulo
- Apariencia IDE/docking (Visual Studio look)

### Document Surface Header estándar

Debe incluir obligatoriamente:
- Botón `←` volver al listado (callback `OnCerrar`)
- Breadcrumb ligero: `Módulo › Título documento`
- Badge de estado del documento
- Fondo `LayerFillColorDefaultBrush`, borde inferior sutil `#E5E5E5`

### Módulos con Document Surface correcto

- ✅ Clientes (ADR-030 Fase 1)
- ✅ Productos (ADR-030 Fase 2)
- ✅ Cotizaciones (ADR-025/027/028 — piloto)
- ✅ Pedidos (ADR-031)
- ✅ Órdenes de Trabajo (ADR-031)
- ✅ Ventas Documentales (ADR-031)

**Documentación completa**: `Documentation/ADR-031-Document-Surface-Visual-Separation-Standard.md`

---

## 18. Regla Crítica — Shared Document Session Pattern (ADR-039)

**Regla institucional OBLIGATORIA**. Aplica a TODOS los documentos con detach/open-in-new-window.

### Principio

> Detach = rehost visual.  
> Detach ≠ nuevo documento.  
> La ventana desacoplada es un contenedor visual alternativo para la misma sesión documental.

### Lo que DEBE preservarse al desacoplar

- Instancia del ViewModel (NO recrear)
- Entidad de Directorio seleccionada
- Chip visual del selector
- Líneas / detalles en memoria
- Totales calculados
- Estado dirty / HasChanges
- Fechas, campos editados, observaciones
- Estatus del documento

### Flujo correcto de rehost (OBLIGATORIO)

```csharp
private void BtnAbrirEnVentana_Click(object sender, RoutedEventArgs e)
{
    var paginaActual = this;

    // Paso 1: Salir del árbol visual inline (OBLIGATORIO antes de entrar al nuevo host)
    EsInlineMode = false;
    VolverALista?.Invoke(); // → DocumentSurfaceContent = null (sincrónico)

    // Paso 2: Rehostear la misma instancia
    _windowManager.OpenWindow<DetachedDocumentWindow, string>(
        key: windowKey,
        factory: () => new DetachedDocumentWindow(paginaActual, titulo));
}
```

### PROHIBIDO en detach

- `new DocumentoPage(dto)` dentro del factory de detach (recrea ViewModel)
- `ViewModel.Initialize(dto)` al rehostear (equivale a recargar desde BD)
- Auto-save antes o durante el detach
- Recargar datos desde BD durante el rehost
- Instanciar un nuevo ViewModel durante detach/attach
- Perder dirty state al cambiar de host visual

### Implementación actual

- ✅ `CotizacionDocumentoPage.BtnAbrirEnVentana_Click` — corregido (ADR-039)
- El resto de documentos (Pedido, Venta, OT) no tienen botón detach actualmente (inline-only per ADR-031). Si se agrega detach a futuro, debe seguir este patrón sin excepción.

---
