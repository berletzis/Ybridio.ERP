# Sesión 2026-05-15 — Commercial Document Surface Pedidos + Bugfixes Críticos

Build final: ✅ 0 errores | BD: YBRIDIO-26 datos corregidos | Sesión extensa

---

## Resumen ejecutivo

Sesión compleja con múltiples implementaciones y corrección de bugs en cadena. El bug más significativo (descuentos no preservados en conversión COT→PED) requirió múltiples iteraciones para encontrar la causa raíz real.

---

## 1. Commercial Document Surface — PedidoDocumentoPage (equivalente a Cotizaciones)

### Cambios Domain
- **`PedidoCargo`** nueva entidad: `ventas.PedidoCargo` (Id, PedidoId, Descripcion, Importe, AplicaIva, Orden)
- **`Pedido.Cargos`**: nueva navegación `ICollection<PedidoCargo>`
- **`PedidoCargoConfiguration`**: EF config con cascade delete
- **`AddPedidoCargo_V1.sql`**: tabla creada en YBRIDIO-26

### Cambios Application
- **`PedidoDto`**: agrega `Cargos?`, `Subtotal?`, nuevos `PedidoCargoDto`, `CrearPedidoCargoDto`
- **`IPedidoService`**: agrega `AgregarCargoAsync`, `EliminarCargoAsync`
- **`PedidoService.CrearAsync`**: usa `CommercialDocumentCalculator`, copia `DescuentoPct`/`IvaAplicable`
- **`PedidoService.AgregarDetalleAsync`**: usa `CommercialDocumentCalculator`, copia `DescuentoPct`/`IvaAplicable`
- **`PedidoService.MapToDto`**: incluye `Sku`, `DescuentoPct`, `IvaAplicable`, `Cargos`, `Subtotal`
- **`ObtenerConDetallesAsync`**: agrega `.ThenInclude(d => d.Producto)` y `.Include(x => x.Cargos)`
- **`PedidoResumenDto`**: agrega `FolioCotizacionOrigen`
- **`PedidoService.ListarAsync`**: proyecta folio de cotización origen

### Cambios WinUI — ViewModel
- `PedidoDocumentoViewModel` completo:
  - `CommercialDocumentCalculator` integrado (`SubtotalBruto`, `Subtotal`, `DescuentoTotal`, `TotalOtrosCargos`, `Impuestos`, `Total`)
  - `Cargos` ObservableCollection + `AgregarCargoAsync` / `EliminarCargoAsync`
  - `DescuentoGlobalPct` + `AplicarDescuentoGlobalALineasAsync`
  - `TieneClienteSeleccionado`, `ClienteEmail`, `ClienteTelefono`
  - `WirarLinea()` pattern (ADR-041)
  - `ActualizarCantidadAsync()` / `ActualizarDescuentoAsync()` con guards IsBusy
  - `CargarConfiguracionFiscalAsync()` (IConfiguracionFiscalService)
  - `RestaurarEntidadSeleccionada()` — hydration de cliente
  - `GetAvailableTransitions()` / `AvanzarAEstatusAsync()` — workflow contextual

### Cambios WinUI — XAML
- `PedidoDocumentoPage.xaml`: Single Document Scroll Pattern completo equivalente a CotizacionDocumentoPage
  - SKU | Descripción | Cantidad (NumberBox) | Desc% (NumberBox) | IVA | Precio | Importe Neto
  - OtrosCargos section con grid y delete
  - Totales completos: SubtotalBruto | (-) Descuento | Subtotal | (+) Cargos | IVA | **Total**
  - StatusBar **dentro del ScrollViewer** (después de Totales — requerimiento del usuario)
  - `BoolToSiNoConverter`: IVA muestra "Sí"/"No" en lugar de "True/False"
  - `HeaderStrip Visibility="Collapsed"` por defecto (solo en inline mode)
  - `OverflowButtonVisibility="Collapsed"` en CommandBar
  - `BtnAbrirEnVentana` en primarios (no SecondaryCommands)

### Cambios WinUI — Code-behind
- `EsInlineMode`: controla HeaderStrip + SepVentana + BtnAbrirEnVentana
- `HidratarSelectorClienteAsync()`: email + teléfono del cliente
- `_listaParaEdicion` guard via `Page.Loaded`: bloquea todos los NumberBox handlers durante render inicial
- Workflow Menu contextual: `WorkflowFlyout_Opening` construye items dinámicamente

### Cambios PedidosPage
- Grid con columna Folio Pedido + Folio Cotización Origen
- StatusBar oculta cuando Document Surface visible (x:Bind + code-behind guard)
- `AbrirPedidoDesdeConversion(PedidoDto)` — abre inline desde conversión

---

## 2. Bug crítico: Descuentos no se preservaban en conversión COT→PED

### Causa raíz — cadena de 3 capas

**Capa 1: EF Core change tracker stale**
- El `ErpDbContext` es Scoped. `CotizacionService.ActualizarDescuentoAsync` hace delete+readd de líneas durante la edición. Al llamar `ConvertirAPedidoAsync`, el identity map podía tener versiones antiguas con valores pre-descuento.
- **Fix**: `AsNoTracking()` para detalles y cargos en la conversión — siempre lee fresh del DB.

**Capa 2: `PedidoService.AgregarDetalleAsync` sin CommercialDocumentCalculator**
- `Importe = Cantidad × PrecioUnitario` sin aplicar descuento; no copiaba `DescuentoPct`/`IvaAplicable`.
- **Fix**: Usa `CommercialDocumentCalculator.CalcularImporteLinea` y copia todos los campos.

**Capa 3: WinUI 3 DataTemplate `ValueChanged` durante render inicial**
- Cuando `PedidoDocumentoPage` se abría, el DataTemplate disparaba `ValueChanged` para cada NumberBox con `OldValue=NaN → NewValue=actual`. Esto llamaba `ActualizarCantidadAsync` que hacía delete+readd con el `AgregarDetalleAsync` roto (Capa 2).
- **Fix definitivo**: `_listaParaEdicion = false` hasta `Page.Loaded`. Bloquea TODOS los handlers hasta que el árbol visual esté completamente renderizado.

**Capa adicional: EF Core `HasDefaultValue` gotcha**
- `HasDefaultValue(0m)` en `PedidoDetalleConfiguration` y `CotizacionDetalleConfiguration` causaba que EF usara `ValueGenerated.OnAdd`, potencialmente omitiendo el campo del INSERT.
- **Fix**: Eliminado `HasDefaultValue` de ambas configuraciones. EF usa `ValueGenerated.Never` implícito.

### Corrección de datos en BD
```sql
UPDATE pd
SET pd.DescuentoPct = cd.DescuentoPct,
    pd.IvaAplicable = cd.IvaAplicable,
    pd.Importe = ROUND(pd.Cantidad * pd.PrecioUnitario * (1 - cd.DescuentoPct/100.0), 2)
FROM ventas.PedidoDetalle pd
JOIN ventas.Pedido p ON pd.PedidoId = p.Id
JOIN ventas.CotizacionDetalle cd ON cd.CotizacionId = p.CotizacionId
    AND cd.ProductoId = pd.ProductoId AND cd.Descripcion = pd.Descripcion
WHERE p.CotizacionId IS NOT NULL
  AND ABS(pd.DescuentoPct - cd.DescuentoPct) > 0.001;
```

---

## 3. Bug: Transparencia en CommandBar al convertir COT→PED

### Causa raíz
`WorkspaceTabView` tenía `Background="{ThemeResource LayerOnMicaBaseAltFillColorDefaultBrush}"` (semi-transparente en Windows 11 Mica) y `Margin="0,12,0,0"`. El ModuleFrame (CotizacionesPage) sangraba a través del background semi-transparente y el gap de 12px.

### Fix (ShellPage.xaml)
```xml
<!-- ANTES -->
Margin="0,12,0,0"
Background="{ThemeResource LayerOnMicaBaseAltFillColorDefaultBrush}"

<!-- DESPUÉS -->
Margin="0,0,0,0"
Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"
```

---

## 4. Bug: Conversión abre Pedido en WorkspaceTab (incorrecto)

### Causa raíz
`AbrirPedidoEnWorkspace` en `CotizacionDocumentoPage` abría el Pedido como WorkspaceTab en lugar de como Document Surface inline en PedidosPage — diferente al comportamiento de abrir desde el grid.

### Fix: Visual Tree Traversal
```csharp
// En CotizacionDocumentoPage:
var ventasPage = EncontrarAncestro<VentasPage>(this);
ventasPage?.AbrirPedidoDesdeConversion(dto);

// En VentasPage:
public void AbrirPedidoDesdeConversion(PedidoDto pedido)
{
    VentasTabs.SelectedItem = TabPedidos;  // activa tab + lazy-load PedidosPage
    if (FramePedidos.Content is PedidosPage p)
        p.AbrirPedidoDesdeConversion(pedido);
}

// En PedidosPage:
public void AbrirPedidoDesdeConversion(PedidoDto pedido)
{
    var page = new PedidoDocumentoPage(pedido);
    page.VolverALista = () => _ = ViewModel.CerrarDocumentSurfaceAsync();
    page.EsInlineMode = true;
    ViewModel.AbrirEditarPedido(page);
}
```

---

## 5. Otros fixes menores

- **ContentDialog COMException**: guard `try/catch` en `AbrirDialogoAgregarCargo` para cuando ya hay un dialog abierto
- **CotizacionDocumentoPage.ActualizarCantidadAsync**: guard `if (IsBusy) return` para evitar concurrencia DbContext
- **TituloDocumento Cotización**: muestra `"Cotización COT-000066"` en lugar de `"Cotización #66"` cuando hay folio
- **`ConvertirAPedidoAsync` retorno DTO**: incluye `DescuentoPct`, `IvaAplicable`, `Cargos`, `Subtotal` completos

---

## Impacto en Auditoría Estructural

### Nuevas validaciones (ya implementadas)
- `WorkflowAuditService.AuditPedidosLifecycleAsync`: detecta estados inválidos, folios duplicados/null
- `WorkflowAuditService.AuditVentasLifecycleAsync`: detecta ventas Cerradas con saldo, overpayment
- `CommercialIntegrityAuditService.AuditFinancialTotalsAsync`: detecta drift Total encabezado vs SUM(detalles)
- `CommercialIntegrityAuditService.AuditPaymentIntegrityAsync`: detecta TotalPagado ≠ SUM(pagos)

### Riesgo de falsos positivos — ELIMINADO
- `HasDefaultValue` eliminado de configuraciones EF: el auditor no verá más inconsistencias de INSERT

### Scripts pendientes — NINGUNO
- `AddPedidoCargo_V1.sql`: ✅ aplicado
- `AddWorkflowColumns_V1.sql`: ✅ aplicado

---

## Próximos pasos

| Feature | Prioridad |
|---|---|
| Validar descuentos en todos los escenarios (grid, manual, conversión) | Inmediato |
| `Page.Loaded` guard — documentar en CLAUDE_RULES.md como patrón estándar | Alta |
| Conversión COT→PED: verificar que cargos se muestran en PedidosPage inline | Alta |
| TituloDocumento en CotizacionesPage grid (columna Folio ya muestra correcto) | Media |
