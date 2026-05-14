# Session Closure — Commercial Discount Pattern, Workflow Refactor, UX Fixes

> **Fecha**: 2026-05-12  
> **Build status**: ✅ 0 errores | **BD**: YBRIDIO-26 | **Branch**: master  
> **ADRs creados/formalizados este día**: ADR-042 (Commercial Discount), ADR-043b (Two-Phase Discount), Commercial Document Workflow Pattern, Selector DTO Hydration Rule, Single Document Session Rule, Operational Date Display Pattern, Global Discount Lifecycle

---

## Resumen ejecutivo

Sesión de correcciones y mejoras operacionales a Cotizaciones. Se implementaron: descuentos por línea y global, refactor del workflow de estados, correcciones críticas de concurrencia DbContext, hidratación correcta del selector de cliente, y Single Document Session Rule para evitar sesiones duplicadas.

---

## 1. Commercial Discount Pattern (ADR-042) ✅

**Problema**: No existían descuentos en Cotizaciones. Los cálculos estaban hardcodeados.

**Implementado**:
- `CommercialDocumentCalculator` — clase estática en `Ybridio.Application/Common/`, SoT de aritmética comercial
- `DescuentoPct decimal(5,2)` añadido a `ventas.CotizacionDetalle` (script SQL ejecutado en YBRIDIO-26)
- `DetalleLineaEditable`: campos `DescuentoPct/DescuentoPctDouble/DescuentoImporte`, `Importe` usa calculator
- Columna `Desc. %` editable inline en el grid (NumberBox)
- Bloque `Descuento Global (%)` en formulario (Col 1, Row 1)
- Totales: `SubtotalBruto` + `DescuentoTotal` (condicionales) + `Subtotal` + IVA + Total
- **Regla no acumulable**: diálogo de confirmación cuando global aplica sobre líneas con descuento individual

**Archivos**: `CommercialDocumentCalculator.cs`, `CotizacionDetalle.cs`, `CotizacionDto.cs`, `CotizacionService.cs`, `CotizacionDocumentoViewModel.cs`, `CotizacionDocumentoPage.xaml/cs`

---

## 2. PermisoService — Fix Concurrencia DbContext ✅

**Problema**: `InvalidOperationException: A second operation was started on this context instance` al navegar rápido.

**Causa**: `PermisoService` usaba `UserManager<ApplicationUser>` cuyo `UserStore` usa el `DbContext` scoped compartido. Múltiples evaluaciones concurrentes → colisión.

**Fix**: Eliminado `UserManager` del servicio. Las queries de roles van directo por el contexto factory aislado (`ctx.UserRoles.Join(ctx.Roles, ...)`).

**Archivo**: `PermisoService.cs`

---

## 3. Two-Phase Discount Apply Pattern (ADR-043b) ✅

**Problema**: `InvalidOperationException: A second operation` al aplicar descuento global con múltiples líneas en doc existente.

**Causa raíz**: `AplicarDescuentoGlobalALineas` llamaba `ActualizarDescuentoAsync` en loop → INPC disparaba `NumberBox_Descuento_ValueChanged` (async void) que llamaba de nuevo al servicio → dos paths concurrentes sobre el mismo `_context`.

**Fix — Dos fases**:
- **Fase 1** (síncrona): set todos los `linea.DescuentoPct = pct` en memoria primero
- **Fase 2** (async, único scope IsBusy): persistir cada línea secuencialmente
- Guard en `ActualizarDescuentoAsync`: `if (linea.DescuentoPct == pctClamped) return;`
- Guard `IsBusy` en handlers del code-behind

---

## 4. Global Discount Lifecycle ✅

**Regla de uniformidad**: `DescuentoGlobalPct` es válido solo cuando todas las líneas tienen el mismo %.

**Implementado**: `InvalidarDescuentoGlobal()` en ViewModel. Se llama silenciosamente cuando:
- Usuario cambia descuento de una línea individual a valor distinto del global
- Usuario agrega línea vía modal con descuento diferente al global

**Bug fix**: La alerta de descuento global aparecía al abrir/editar cotización. **Causa**: x:Bind OneWay despacha la actualización del NumberBox en el siguiente ciclo del DispatcherQueue (DESPUÉS de que `_hidratandoUI = false`). **Fix**: Guard `if ((decimal)args.NewValue == ViewModel.DescuentoGlobalPct) return;`

---

## 5. Selector de Cliente — Correcciones Múltiples ✅

### Bug 1: Nombre en textbox
`RelacionComercialSelectorControl` rediseñado con dos estados mutuamente excluyentes:
- Estado A (sin selección): `InputBorder` visible (textbox búsqueda)
- Estado B (con selección): `InputBorder` colapsado + `EntityChipPanel` visible debajo (nombre + badge + limpiar)

**La entidad NUNCA se renderiza dentro del InputBorder/TextBox.**

### Bug 2: DTO sintético incorrecto (Selector DTO Hydration Rule)
`Initialize()` creaba DTO parcial: `EntityType = Empresa` hardcodeado, `EmpresaComercialId = RelacionComercialId` (mapping erróneo), sin Email/Teléfono.

**Fix**: 
- `IDirectorioService.ObtenerDtoParaSelectorAsync(relacionComercialId)` — nuevo método que carga `RelacionComercial` con navegación y retorna DTO completamente hidratado
- `RestaurarEntidadSeleccionada(dto)` en ViewModel — sin marcar dirty
- `HidratarSelectorClienteAsync(relacionId)` en page — fire-and-forget post-Initialize

---

## 6. Mejoras UX Cotización ✅

- **CalendarDatePicker**: Reemplazado DatePicker por CalendarDatePicker + etiqueta "8 Junio 2026" via `OperationalDateConverter`
- **SKU visible**: `ThenInclude(d => d.Producto)` en `ObtenerConDetallesAsync`, `Sku: d.Producto?.Codigo` en `MapToDto`
- **Labels semánticos**: "Importe Neto" (columna grid), "Subtotal sin descuentos" (totales)
- **CommandBar reordenado**: [Guardar]\|[Líneas]\|[Aprobar][Convertir][Cancelar]\|[Enviar]

---

## 7. Commercial Document Workflow Pattern ✅

**Problema**: "Enviada" como estado comercial generaba flujo incorrecto: Borrador → Enviada → Aprobada. "Enviar" tratado como estado, no como acción.

**Nuevo flujo**: `Borrador → Aprobada → Convertida | Cancelada`

- `EstatusCotizacion.Enviada = 1` → marcado `[Obsolete]`, mantenido por compatibilidad BD
- `EstatusCotizacion.Convertida = 3` → nuevo estado terminal al convertir a Pedido
- `ConvertirAPedidoAsync` ahora marca `Estatus = Convertida`
- `BtnEnviar_Click` = acción operacional (stub sin cambio de estado)
- `PuedeAprobar` = desde Borrador (no Enviada)
- `PuedeEnviar` = desde cualquier estado activo (acción reutilizable)

---

## 8. Single Document Session Rule ✅

**Problema**: Al desacoplar Cot 123 a ventana OS y volver al grid, doble-click creaba nueva instancia ignorando la ventana existente → dos ViewModels, dos dirty states.

**Implementado**:
- `IWindowManager.TryActivateWindow(string documentKey)` — busca ventana activa por key de documento sin necesitar el tipo genérico; si existe: activa, retorna true
- `CotizacionesPage._currentInlineDocumentId` — tracking de sesión inline
- `AbrirCotizacionInline`: 3 checks antes de crear instancia (detached? → inline duplicado? → crear nuevo)

**Diseñado para reutilización**: funciona igual para Pedidos, Ventas, OT.

---

## Estado final del build

```
Build: ✅ 1 succeeded, 0 failed
Errors: 0
Warnings: 241 (todos MVVMTK0045 preexistentes — KI-003)
Platform: x64, Debug
BD: ✅ DescuentoPct ejecutado en YBRIDIO-26
```

---

## Próximos pasos recomendados

| Prioridad | Feature | Notas |
|---|---|---|
| Alta | Aplicar Single Document Session Rule a Pedidos, Ventas, OT | Mismo patrón que Cotizaciones |
| Alta | Migración `AddBusinessPartnerModel` (KI-018) | `dotnet ef database update` en YBRIDIO-26 |
| Media | Directorio UX — páginas PersonasPage/EmpresasPage (KI-017) | Document Surface pattern |
| Media | `IEntradaService.CrearAsync` / `ISalidaService.CrearAsync` (KI-002) | Botones Nuevo son stubs |
| Baja | Migrar CalendarDatePicker + OperationalDateConverter a Pedidos, OT | Mismo patrón |
| Baja | `FiscalConstants.TasaIvaEstandar` — migrar a config por empresa | Actualmente constante fija |
