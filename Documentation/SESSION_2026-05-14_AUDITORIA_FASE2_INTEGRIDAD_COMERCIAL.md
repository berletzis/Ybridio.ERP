# Sesión 2026-05-14 — Auditoría Fase 2: Integridad Comercial

Build final: ✅ 0 errores | BD: YBRIDIO-26 | Validadores ejecutados contra datos reales

---

## Resumen ejecutivo

Evolución del módulo de Auditoría de Datos a un sistema de integridad comercial operacional:
- `SchemaAuditEntry` extiende con propiedad `Module` para grouping y filtro por módulo
- `CommercialIntegrityAuditService` — 7 validadores financieros/comerciales
- Panel ejecutivo de chips por módulo en UI
- Columna Módulo en grid de hallazgos
- Filtro por módulo en CommandBar
- Nuevo botón "Integridad Comercial"
- `SchemaAuditReport.GetModuleBreakdown()` y `GetModules()`

---

## Resultados reales contra YBRIDIO-26 (2026-05-14)

| Validador | Resultado | Severidad |
|---|---|---|
| Pedidos referencian COT no Convertida | 0 | ✅ Info |
| COT con múltiples Pedidos activos | **2** | ⚠ Warning — verificar |
| Ventas con Pedido inválido | 0 | ✅ Info |
| Drift Total COT vs PED > 1% | **1** | ⚠ Warning — posible descuento conversión |
| Pedido.Total != SUM(detalles) | 0 | ✅ Info |
| Venta.Total != SUM(detalles) | 0 | ✅ Info |
| Cotizacion.Subtotal != SUM(detalles) | 0 | ✅ Info |
| PagoVenta.Monto <= 0 | 0 | ✅ Info |
| Venta.TotalPagado != SUM(pagos) | 0 | ✅ Info — contador sincronizado |
| Ventas Pagadas con saldo > 0 | 0 | ✅ Info |
| Ventas crédito sin CxC | 0 | ✅ Info |

**Resumen: 0 Critical | 0 Error | 2 Warning | resto Info**

Los 2 warnings son actionable y semánticamente correctos — no falsos positivos.

---

## Cambios implementados

### `SchemaAuditEntry.cs`
- Nueva propiedad `Module string?` (optional, backward-compatible)
- `SchemaAuditReport.GetModuleBreakdown()` — dict Module → (Critical+Error count)
- `SchemaAuditReport.GetModules()` — lista módulos únicos
- `ToJsonString()` incluye Module en la serialización

### `CommercialIntegrityAuditService.cs` (NUEVO)

7 validadores con findings usando `Module`:

| Validador | Módulo | Qué detecta |
|---|---|---|
| `AuditConversionChainAsync` | Cotizaciones/Pedidos/Ventas | Cadena COT→PED→VTA coherente |
| `AuditFinancialTotalsAsync` | Cotizaciones/Pedidos/Ventas | Total encabezado = SUM(detalles) |
| `AuditPaymentIntegrityAsync` | Pagos | Monto válido, TotalPagado=SUM, FormaPago |
| `AuditDocumentAgingAsync` | Cotizaciones/Pedidos/Ventas | Documentos estancados + distribución estados |
| `AuditProductReferencesAsync` | Cotizaciones/Pedidos/Ventas | ProductoId válido en detalles |
| `AuditCreditAndCxCAsync` | CxC | Ventas crédito con CxC, CxC para canceladas |
| `AuditTrailReadinessAsync` | General | Infraestructura audit trail, FechaCreacion, UsuarioCreacionId |

### `ICommercialIntegrityAuditService.cs` (NUEVO)
Interface del servicio.

### `App.xaml.cs`
- `services.AddTransient<ICommercialIntegrityAuditService, CommercialIntegrityAuditService>()`

### `AuditoriaViewModel.cs`
- `ICommercialIntegrityAuditService` inyectado
- `ModuleMetric` record: Module, Critical, Errors, Total
- `ObservableCollection<ModuleMetric> ModuleMetrics`
- `ObservableCollection<string> ModulosDisponibles`
- `[ObservableProperty] string moduloFiltroLabel`
- Comando `EjecutarAuditoriaComercialAsync`
- Comando `FiltrarModuloCommand` — filtro adicional por módulo
- `ActualizarModuleMetrics()` — calcula chips del panel ejecutivo
- `AplicarFiltro()` — ahora aplica AMBOS filtros (severidad AND módulo)
- `_filtroSeveridad` + `_filtroModulo` reemplazan `_filtroActual`
- `AuditEntryRow.Module` — nueva propiedad desde `entry.Module ?? "General"`

### `AuditoriaPage.xaml`
- Botón "Integridad Comercial" (glyph &#xE8C8;)
- Filtro por módulo (MenuFlyout con Cotizaciones/Pedidos/Ventas/Pagos/CxC/General)
- Panel ejecutivo Row=2: chips por módulo con Critical/Errors/Total
- Grid añade columna Módulo (Width=100) entre Severidad y Categoría
- Header: Severidad | Módulo | Categoría | Mensaje | Sugerencia
- StatusBar y ProgressRing en Row=4

---

## Findings reales restantes (2 warnings)

### Warning 1: 2 cotizaciones con múltiples pedidos
- Cotizaciones que generaron más de 1 Pedido activo
- Puede ser intencional (entregas parciales) o error operacional
- **Acción**: verificar con usuario si fue intencional

### Warning 2: 1 pedido con drift de total >1% vs cotización origen
- Posiblemente el Pedido se generó antes de que el Importe incluyera descuentos
- El Importe en PedidoDetalle preserva el importe ya descontado desde la cotización
- **Acción**: revisar ese Pedido específico

---

## Próximos pasos

| Feature | Prioridad |
|---|---|
| Ejecutar "Integridad Comercial" desde UI y validar con usuario | Inmediato |
| Investigar las 2 cotizaciones con múltiples pedidos | Alta |
| Investigar el pedido con drift de total | Alta |
| Agregar validadores para Compras (OrdenCompra, RecepcionCompra) | Media |
| Agregar validadores para Finanzas (CxP, gastos, ingresos) | Media |
| Implementar tabla de audit trail real | Baja |
