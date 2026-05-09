# Ventas Operativas — Sales Core PYME

> Implementado: 2026-05-08  
> Build: ✅ 0 errores

## Objetivo

Núcleo operacional de ventas para miniERP PYME. Soporta dos flujos principales:

- **Venta Simple**: Cotización → Venta → Cobro → Entrega *(retail, mostrador, distribución)*
- **Servicio / OT**: Cotización → Pedido → OrdenTrabajo → Ejecución → Entrega → Cobro *(talleres, servicios técnicos, reparación)*

**NO incluye**: manufactura enterprise, MRP, BOM, rutas de producción, SAT/facturación fiscal.

---

## Módulos del Sales Core

| Tab | Descripción |
|---|---|
| **Clientes** | CRUD completo. Preparado para CRM ligero futuro. |
| **Cotizaciones** | Propuesta comercial con ciclo de vida: Borrador → Enviada → Aprobada \| Cancelada |
| **Pedidos** | Compromiso operacional: Nuevo → Confirmado → EnProceso → Completado \| Cancelado |
| **Órdenes de Trabajo** | OT ligera: Nueva → EnProceso → [EsperandoMaterial →] Terminada → Entregada \| Cancelada |

---

## Entidades Domain (`Ybridio.Domain.Ventas/`)

### Nuevas entidades

| Entidad | Tabla | Hereda | Descripción |
|---|---|---|---|
| `EstatusCotizacion` (enum) | — | — | Borrador(0) Enviada(1) Aprobada(2) Cancelada(9) |
| `EstatusPedido` (enum) | — | — | Nuevo(0) Confirmado(1) EnProceso(2) Completado(3) Cancelado(9) |
| `EstatusOrdenTrabajo` (enum) | — | — | Nueva(0) EnProceso(1) EsperandoMaterial(2) Terminada(3) Entregada(4) Cancelada(9) |
| `Cotizacion` | `ventas.Cotizacion` | AuditableEntity | Propuesta con Detalles, NombreCliente denormalizado |
| `CotizacionDetalle` | `ventas.CotizacionDetalle` | — | Línea de ítem: producto del catálogo o servicio ad-hoc |
| `Pedido` | `ventas.Pedido` | AuditableEntity | Compromiso operacional, puede venir de Cotizacion |
| `PedidoDetalle` | `ventas.PedidoDetalle` | — | Línea de ítem igual que CotizacionDetalle |
| `OrdenTrabajo` | `ventas.OrdenTrabajo` | AuditableEntity | OT ligera con Descripcion, FechaCompromiso, ResponsableId |
| `OrdenTrabajoMaterial` | `ventas.OrdenTrabajoMaterial` | — | Material o servicio usado en la OT |

### Entidad extendida

| Entidad | Cambio |
|---|---|
| `Cliente` (catalogos.Cliente) | +Telefono, +Direccion, +Notas, +LimiteCredito |

---

## Fórmulas y Cálculos

| Fórmula | Expresión | Persiste | Razón |
|---|---|---|---|
| `DetalleLinea.Importe` | `Cantidad × PrecioUnitario` | ✅ BD | Histórico: precio puede cambiar; se congela al crear |
| `Cotizacion.Total` | `SUM(detalles.Importe)` | ✅ BD | Acceso rápido en listados sin cargar detalles |
| `Cotizacion.Subtotal` | `SUM(detalles.Importe)` (V1 = Total) | ✅ BD | Preparado para IVA independiente en V2 |
| `Pedido.Total` | `SUM(detalles.Importe)` | ✅ BD | Idem |
| `OrdenTrabajo.Total` | `SUM(materiales.Importe)` | ✅ BD | Recalculado y persistido al agregar/quitar materiales |
| `OTResumenDto.EsUrgente` | `FechaCompromiso ≤ hoy+1 AND estatus activo` | ❌ Runtime | Depende de la fecha actual |
| `SaldoCliente` | `SUM(CxC.SaldoPendiente WHERE Cliente)` | ❌ Runtime | CxC no tiene FK a Cliente en V1; se calculará en V1.1 |

---

## Permisos

| Módulo | Clave | Descripción |
|---|---|---|
| cotizacion | `cotizacion.ver` | Ver cotizaciones |
| cotizacion | `cotizacion.crear` | Crear cotizaciones |
| cotizacion | `cotizacion.editar` | Cambiar estado (Enviada/Aprobada) |
| cotizacion | `cotizacion.cancelar` | Cancelar o eliminar |
| pedido | `pedido.ver` | Ver pedidos |
| pedido | `pedido.crear` | Crear pedidos |
| pedido | `pedido.editar` | Avanzar estados |
| pedido | `pedido.cancelar` | Cancelar o eliminar |
| ordentrabajo | `ordentrabajo.ver` | Ver OTs |
| ordentrabajo | `ordentrabajo.crear` | Crear OTs |
| ordentrabajo | `ordentrabajo.actualizar` | Agregar materiales, cambiar estados intermedios |
| ordentrabajo | `ordentrabajo.cerrar` | Marcar Terminada o Entregada |

Permisos existentes reutilizados: `cliente.ver`, `cliente.crear`, `cliente.editar`, `venta.ver`, `venta.crear`

---

## Servicios Application

| Interfaz | Auth validado | Descripción |
|---|---|---|
| `IClienteService` + `ClienteService` | cliente.crear/editar | CRUD extendido de clientes |
| `ICotizacionService` + `CotizacionService` | cotizacion.* | CRUD + cambio de estatus |
| `IPedidoService` + `PedidoService` | pedido.* | CRUD + avanzar estado |
| `IOrdenTrabajoService` + `OrdenTrabajoService` | ordentrabajo.* | CRUD + materiales + estados |

Todos reutilizan `IErpAuthorizationService.PuedeAsync(PermisosClave.X.Y)` — doble capa: ViewModel + Service.

---

## Patrón NombreCliente denormalizado

Cotizacion, Pedido y OrdenTrabajo almacenan `NombreCliente` como texto además del `ClienteId?` nullable.

**Razón**: permite clientes de mostrador (sin registro en BD), y congela el nombre exacto al momento de crear el documento aunque el cliente cambie de nombre posteriormente.

---

## Integración con módulos existentes

| Integración | Estado |
|---|---|
| Descuento de inventario al generar Venta | ✅ VentaService existente |
| Materiales OT → descuento inventario | ⚠ V1.1 — campo informativo por ahora |
| Pagos → CxC de Finanzas | ⚠ V1.1 — requiere ClienteId en CxC |
| Cotizacion → genera Pedido automático | ⚠ V1.1 — por ahora manual |

---

## Observabilidad

Los 4 ViewModels implementan `BuildOperationalContext()` con:
- `Module: "Ventas"`, `SubModule: "Clientes"/"Cotizaciones"/"Pedidos"/"OrdenesTrabajo"`
- `Notes: ["ACCESO DENEGADO — permiso: x.y"]` cuando el permiso falla
- `EmpresaFilter: Applied`, scopes documentados en Notes

---

## Limitaciones V1 / Pendientes

| Item | Estado |
|---|---|
| Crear cotizaciones desde UI (formulario completo) | Pendiente — botón "Nuevo" no abre formulario completo aún |
| Crear pedidos desde UI | Pendiente — solo avanzar estado de existentes |
| SaldoCliente en tiempo real | Pendiente — requiere FK ClienteId en CxC |
| Integración OT → descuento inventario | V1.1 |
| Conversión Cotizacion → Pedido automática | V1.1 |
| WorkspaceService para documentos individuales | V1.1 |
