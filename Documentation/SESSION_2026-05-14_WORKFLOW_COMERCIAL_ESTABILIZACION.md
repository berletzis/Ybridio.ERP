# Sesión 2026-05-14 — Estabilización Workflow Comercial + Identidad Documental + UX Operacional

Build final: ✅ 0 errores | BD: requiere AddWorkflowColumns_V1.sql | Docs: actualizados

---

## Resumen ejecutivo

Estabilización completa del workflow comercial documental (Cotización → Pedido → Venta → Cierre):
- Folios generados en Pedido y Venta (antes solo en Cotización)
- Enums de estado formalizados con lifecycle completo
- Bloqueo operacional correcto por estado
- Status capsules institucionales con colores por estado
- CommandBar con IsEnabled por workflow guard
- StatusBar mejorado con folio y totales
- CerrarVenta implementado (estado Cerrada formal)
- Fix: VentaDetalle incluye nombre de producto (ThenInclude Producto)
- Fix: ConvertirAPedidoAsync genera folio para el Pedido resultante
- Fix: Cotización bloqueada al agregar/eliminar detalles si no está en Borrador
- CLAUDE.md actualizado: directiva de lectura obligatoria de documentación

---

## Cambios Domain

### EstatusPedido — renombrado + Parcial
- `Nuevo(0)` → `Borrador(0)` — edición activa
- `Confirmado(1)` → `Autorizado(1)` — aprobado para procesar
- `EnProceso(2)` → `EnProceso(2)` — sin cambio
- `Completado(3)` → `Finalizado(3)` — completado
- NEW: `Parcial(4)` — cumplimiento parcial
- `Cancelado(9)` → `Cancelado(9)` — sin cambio

**Impacto BD**: Ninguno (solo rename de miembros C# — valores int iguales).

### EstatusVenta — estados completos
- `Borrador(0)` — sin cambio (pre-confirmación)
- `Confirmada(1)` → `PendientePago(1)` — renombrado, mismo valor DB
- NEW: `Pagada(2)` — auto-transición cuando TotalPagado ≥ Total
- NEW: `Facturada(3)` — comprobante fiscal emitido
- NEW: `Entregada(4)` — entrega completada
- NEW: `Cerrada(5)` — cierre formal (acción explícita del usuario)
- `Cancelada(9)` — sin cambio

**Impacto BD**: Ninguno (nuevos valores int no impactan registros existentes).

### PedidoDetalle — nuevas columnas
- `DescuentoPct DECIMAL(5,2) DEFAULT 0` — preserva descuento de cotización origen
- `IvaAplicable BIT DEFAULT 1` — preserva flag IVA de cotización origen

### Pedido — nueva columna
- `Subtotal DECIMAL(18,2) NULL` — separación subtotal/total para futuros cargos

**Script BD requerido**: `Documentation/Scripts/AddWorkflowColumns_V1.sql`

---

## Cambios Application

### PedidoService
- Inyecta `IFolioGeneratorService` — genera folio en `CrearAsync`
- Bloqueo en `ActualizarAsync`: error si estado es Finalizado o Cancelado
- Mappers actualizados: incluyen `Folio` en `PedidoResumenDto` y `PedidoDto`
- `EstatusTexto` actualizado con nuevos valores

### VentaDocumentalService
- Inyecta `IFolioGeneratorService` — genera folio en `CrearAsync`
- `ObtenerConDetallesAsync`: agrega `.ThenInclude(d => d.Producto)` — fix nombre producto
- `RegistrarPagoAsync`: auto-transición `PendientePago → Pagada` cuando saldo = 0
- NEW: `CerrarAsync` — transiciona a `Cerrada`, valida saldo = 0
- `CancelarAsync`: ahora permite cancelar desde cualquier estado excepto `Cerrada/Cancelada`
- Mappers: incluyen `Folio`; `EstatusVentaTexto` helper con todos los estados

### CotizacionService (ConvertirAPedidoAsync)
- Genera folio propio para el Pedido resultante (`TipoDocumentoSerie.Pedido`)
- Observación del pedido incluye folio de la cotización origen cuando existe
- Bloqueo: `AgregarDetalleAsync` y `EliminarDetalleAsync` requieren estado Borrador
- Bloqueo: `ActualizarAsync` bloquea si estado Aprobada (cotización congelada)

### IVentaDocumentalService
- Agrega contrato `CerrarAsync`
- Actualiza doc summary con ciclo completo

### DTOs actualizados
- `PedidoResumenDto` + `PedidoDto`: nuevo campo `string? Folio` (opcional, default null)
- `VentaDocumentalResumenDto` + `VentaDocumentalDto`: nuevo campo `string? Folio`

### EF Configurations actualizadas
- `PedidoDetalleConfiguration`: columns `DescuentoPct`, `IvaAplicable`
- `PedidoConfiguration`: column `Subtotal`

---

## Cambios WinUI

### Nuevos converters
- `EstatusPedidoToBgBrushConverter` / `EstatusPedidoToFgBrushConverter`
- `EstatusVentaToBgBrushConverter` / `EstatusVentaToFgBrushConverter`

Paleta de colores por estado:
| Estado         | Background   | Texto      |
|----------------|-------------|------------|
| Borrador (P/V) | #F0F0F0     | #8A8A8A    |
| Autorizado (P) | #EBF3FB     | #0078D4    |
| EnProceso (P)  | #FFF4CE     | #8A6400    |
| Parcial (P)    | #FEE5CA     | #8A3800    |
| Finalizado (P) | #E0F2E6     | #008040    |
| PendientePago  | #FFF4CE     | #8A6400    |
| Pagada         | #EBF3FB     | #0078D4    |
| Facturada      | #E0F2E6     | #008040    |
| Entregada      | #CDEDDB     | #006030    |
| Cerrada        | #B4E1C8     | #004020    |
| Cancelado (P/V)| #F8F8F8     | #A0A0A0    |

### PedidoDocumentoViewModel
- Nueva propiedad `Folio` (`[ObservableProperty] string? folio`)
- `TituloDocumento`: usa folio cuando disponible (`"Pedido PED-000001"`)
- Workflow guards actualizados con nuevos enum values
- `PuedeEditar`: bloquea en Finalizado o Cancelado
- `PuedeAvanzar`: permite desde Borrador, Autorizado, EnProceso, Parcial
- `PuedeGenerarOT`: desde Autorizado en adelante
- `PuedeCancelar`: cualquier estado excepto Finalizado/Cancelado
- `PuedeGenerarVenta`: cualquier estado no-Cancelado
- `SiguienteEstatus`: Borrador→Autorizado→EnProceso→Finalizado (Parcial→Finalizado)

### VentaDocumentoViewModel
- Nueva propiedad `Folio`
- `TituloDocumento`: usa folio cuando disponible
- `EstatusTexto`: switch completo con todos los estados
- `PuedeRegistrarPago`: desde PendientePago o Pagada
- `PuedeCerrar`: cuando saldo = 0 y estado ≥ Pagada
- `PuedeCancelar`: cualquier estado excepto Cerrada/Cancelada
- NEW: `CerrarVentaAsync()` — invoca `IVentaDocumentalService.CerrarAsync`

### PedidoDocumentoPage.xaml
- Status capsule dinámico (colores por estado vía converters)
- CommandBar con Border institucional (background LayerFillColorDefaultBrush)
- Botones con `IsEnabled` ligados a workflow guards
- StatusBar mejorado: muestra folio + líneas + total

### VentaDocumentoPage.xaml
- Status capsule dinámico (colores por estado vía converters)
- CommandBar con Border institucional
- Botones con `IsEnabled` ligados a workflow guards
- NEW: botón "Cerrar Venta" (`BtnCerrarVenta_Click`)
- StatusBar mejorado: muestra folio + líneas + total + saldo

### VentaDocumentoPage.xaml.cs
- `BtnCerrarVenta_Click`: diálogo de confirmación + `CerrarVentaAsync()`
- `BtnCancelarVenta_Click`: texto mejorado

---

## Script BD requerido

Ejecutar **antes** de desplegar:
```sql
-- Documentation/Scripts/AddWorkflowColumns_V1.sql
-- Agrega: PedidoDetalle.DescuentoPct, PedidoDetalle.IvaAplicable, Pedido.Subtotal
```

---

## Instrucción CLAUDE.md actualizada

Se agregó directiva al inicio de CLAUDE.md:
> "Al iniciar cada sesión, leer: CLAUDE_RULES.md, ARCHITECTURE_STATUS.md, SESSION_*.md más reciente, KNOWN_ISSUES.md"

---

## Próximos pasos

| Feature | Prioridad |
|---|---|
| Ejecutar `AddWorkflowColumns_V1.sql` en YBRIDIO-26 | BLOCKER para PedidoDetalle con descuentos |
| Migrar status capsules a Cotizaciones (Aprobada=Ámbar como requerimiento) | Alta |
| Agregar Folio en grids de listado (PedidosPage, VentasDocumentalesPage) | Alta |
| Implementar orden DESC en grids de Pedidos/Ventas (ya implementado en servicios) | Verificar |
| Implementar bloqueo visual de edición líneas en CotizacionDocumentoPage cuando Aprobada | Media |
| Máscara monetaria institucional (Fase 3 requerimiento) | Media |
| Single Document Session: actualizar identity con folio en Pedido/Venta | Media |
