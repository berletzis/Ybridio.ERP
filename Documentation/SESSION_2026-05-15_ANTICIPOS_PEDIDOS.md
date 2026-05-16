# SESSION 2026-05-15 — Anticipos, Pagos y Control Financiero Operacional sobre Pedidos

## Estado: Build ✅ 0 errores | BD YBRIDIO-26 ✅ actualizada

---

## Fases implementadas

### FASE 1 — Auditoría infraestructura existente
Inventario completo antes de crear nada:
- 8 entidades financieras existentes en `Ybridio.Domain/Finanzas/`
- `PagoVenta` como patrón de referencia para `AnticipoPedido`
- 25 permisos financieros — reutilizado `pedido.editar` para anticipos
- Decisión: `AnticipoPedido` = nueva entidad análoga a `PagoVenta`, sin tocar `MovimientoCaja`

### FASE 2 — Modelo financiero Pedido

**Nuevo enum** `EstadoFinancieroPedido`:
- `SinPago=0`, `AnticipoParcial=1`, `AnticipoCompleto=2`, `ParcialmentePagado=3`, `Liquidado=4`

**Nuevas columnas en `ventas.Pedido`**:
- `AnticipoRequerido DECIMAL(18,2) NULL` — monto mínimo configurable por pedido
- `AnticipoPagado DECIMAL(18,2) NOT NULL DEFAULT 0` — acumulado automático
- `EstadoFinanciero INT NOT NULL DEFAULT 0` — calculado en cada operación

**Archivos modificados**:
- `Ybridio.Domain/Ventas/EstadoFinancieroPedido.cs` — nuevo enum
- `Ybridio.Domain/Ventas/Pedido.cs` — 3 nuevos campos + navegación `Anticipos`
- `Ybridio.Infrastructure/Persistence/Configurations/Ventas/PedidoConfiguration.cs`

### FASE 3 — AnticipoPedido entity

**Nueva entidad** `AnticipoPedido : AuditableEntity`:
- Campos: `Id, PedidoId, Fecha, Monto, FormaPago, Referencia` + auditoría completa
- `FK_AnticipoPedido_Pedido` con `ON DELETE CASCADE`

**Archivos nuevos**:
- `Ybridio.Domain/Ventas/AnticipoPedido.cs`
- `Ybridio.Infrastructure/Persistence/Configurations/Ventas/AnticipoPedidoConfiguration.cs`
- `Documentation/Scripts/AddAnticiposPedido_V1.sql` — ejecutado en YBRIDIO-26

**DbContext**: agregado `DbSet<AnticipoPedido> AnticipoPedidos`

**Nuevos DTOs**: `AnticipoPedidoDto`, `RegistrarAnticipoDto`, `EstablecerAnticipoRequeridoDto`

**Nuevos métodos IPedidoService**:
- `RegistrarAnticipoAsync(pedidoId, dto, usuarioId)` — valida `pedido.editar`
- `ListarAnticiposAsync(pedidoId)` — valida `pedido.ver`
- `EstablecerAnticipoRequeridoAsync(pedidoId, monto, usuarioId)` — valida `pedido.editar`

**Lógica financiera centralizada**:
- `PedidoService.CalcularEstadoFinanciero(anticiRequerido, anticipoPagado, total)` — `public static`
- Single Source of Truth para clasificación de estado financiero

### FASE 4 — OT condicionada por anticipo

**Guard en `PedidoService.GenerarOrdenTrabajoAsync`** (service layer):
```csharp
if (p.AnticipoRequerido > 0 && p.AnticipoPagado < p.AnticipoRequerido)
    return ServiceResult.Fail($"Anticipo insuficiente. Requerido: {anticiRequerido:C2}. Pagado: {anticipoPagado:C2}.");
```

**Guard en `PedidoDocumentoViewModel.PuedeGenerarOT`** (VM layer):
- Computed property devuelve false si anticipo insuficiente
- 3 capas: Service + ViewModel + UI (IsEnabled binding)

### FASE 5 — Generar Venta consume anticipos

**Corrección de workflow** en `VentaDocumentalService.GenerarDesdePedidoAsync`:
- Guard por estado: Borrador → error, Finalizado/Cancelado → error
- Anticipo aplicado: `venta.TotalPagado = Math.Min(pedido.AnticipoPagado, total)` al crear la venta

**`PuedeGenerarVenta` corregida** (antes permitía Finalizado):
```
Borrador: No ← CORREGIDO
Autorizado: Sí
EnProceso: Sí
Parcial: Sí
Finalizado: No ← CORREGIDO
Cancelado: No
```

### FASE 7 — Permisos

- Reutilizado `pedido.editar` para registrar/establecer anticipos — sin nuevos permisos
- 3 capas de validación: Service (PuedeAsync) + ViewModel (guard) + UI (IsEnabled)

### FASE 8 — Document Surface Financiera

**Nueva sección en `PedidoDocumentoPage.xaml`** (Row 5, antes del StatusBar):
- Badge `EstadoFinanciero` con colores via `EstadoFinancieroPedidoBadgeConverters.cs`
- Resumen numérico: Total / AnticipoRequerido / AnticipoPagado / SaldoPendiente
- ListView historial de `AnticipoPedidoDto`
- Botón "Registrar Anticipo" → `BtnRegistrarAnticipo_Click` → `ContentDialog` formulario

**ViewModel additions**:
- `Anticipos ObservableCollection<AnticipoPedidoDto>`
- `AnticiRequerido`, `AnticipoPagado`, `EstadoFinanciero`, `EstadoFinancieroTexto`
- `SaldoPendienteFinanciero` (computed)
- `PuedeRegistrarAnticipo` (guard)
- `RegistrarAnticipoAsync()` — actualiza colección observable y recalcula estado
- `EstablecerAnticipoRequeridoAsync()` — idem

---

## ADR generados

- **ADR-065**: Anticipos sobre Pedidos — Dimensión Financiera Independiente
  - `AnticipoPedido` análogo a `PagoVenta`, no toca `MovimientoCaja`
  - `EstadoFinancieroPedido` independiente de `EstatusPedido`
  - `CalcularEstadoFinanciero` = Single Source of Truth financiero
  - Anticipo se "consume" al generar Venta: `venta.TotalPagado = pedido.AnticipoPagado`

---

## Estado final

| Métrica | Valor |
|---|---|
| Build | ✅ 0 errores |
| BD YBRIDIO-26 | ✅ Columnas + tabla aplicadas |
| Nuevos archivos | 5 (domain, config, script, converter, session doc) |
| Archivos modificados | 11 |
| Permisos nuevos | 0 (reutiliza pedido.editar) |
| Architecture Drift | 0 |

---

## Pendientes para próxima sesión

1. `EstablecerAnticipoRequerido` — UI: NumberBox inline en la sección financiera para configurar el monto requerido
2. Replicar sección financiera en vista de lista `PedidosPage` (columnas AnticipoPagado, EstadoFinanciero)
3. Integración opcional `MovimientoCaja` al registrar anticipo (cuando el pago es en efectivo)
4. Venta `VentaDocumentoPage` — mostrar "Anticipo recibido desde Pedido" en resumen de pagos
5. KI-017/KI-018 — Directorio UX
