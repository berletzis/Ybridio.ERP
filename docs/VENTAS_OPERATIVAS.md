# Ventas Operativas — Sales Core PYME

> Implementado: 2026-05-08  
> Extendido con Document Workflow UX: 2026-05-08  
> **Sales Transaction Layer (Venta → Cobro → CxC → Inventario): 2026-05-09**  
> **Workflow Actions Layer (Pedido → Venta, OT → Entregada, Navegación Cruzada): 2026-05-09**  
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
| **Ventas** | Venta documental PYME: Borrador → Confirmada (descuenta inventario, genera CxC crédito) \| Cancelada |
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

## Integración Operacional Real — Cotizaciones (implementado 2026-05-xx)

La cotización evolucionó de captura manual de texto libre a flujo ERP real conectado a catálogos existentes.

### Selección de Cliente real

| Aspecto | Detalle |
|---|---|
| Campo | `AutoSuggestBox` — búsqueda por nombre, RFC o email (mínimo 2 caracteres) |
| Servicio | `IClienteService.BuscarAsync(empresaId, termino)` — valida `cliente.ver` |
| Al seleccionar | Carga `NombreCliente`, `ClienteEmail`, `ClienteTelefono`, `ClienteLimiteCredito` (solo lectura) |
| Validación | `ClienteId` es **obligatorio** para guardar — ya no se acepta texto libre |
| Límite crédito | Informativo en V1 — enforcement en V1.1 (requiere FK ClienteId en CxC) |

### Selección de Producto real

| Aspecto | Detalle |
|---|---|
| Campo | `AutoSuggestBox` en diálogo "Agregar Línea" — búsqueda por código o nombre |
| Servicio | `IProductoService.BuscarAsync(empresaId, termino)` |
| Al seleccionar | Autocarga descripción, SKU (`Codigo`), `Precio` base, unidad medida |
| Producto obligatorio | No se permite agregar líneas sin `ProductoId` del catálogo |

### Existencia informativa (§3 requerimiento)

| Aspecto | Detalle |
|---|---|
| Servicio | `IInventarioService.ListarExistenciasAsync(empresaId)` — suma por empresa |
| Muestra | En diálogo de línea al seleccionar producto; en columna "Existencia" del grid |
| Reserva stock | **NO** — solo orientativo para el vendedor (V1) |
| Descuento | **NO** — inventario solo se descuenta al generar Venta (`VentaService`) |

### Campos extendidos en `DetalleLineaEditable`

| Campo | Tipo | Persiste | Razón |
|---|---|---|---|
| `Sku` | `string?` | ❌ Runtime | SKU del producto al momento de crear la línea; orientativo |
| `ExistenciaDisponible` | `decimal?` | ❌ Runtime | Suma existencias empresa al momento de cargar; no reserva stock |

### Fórmulas adicionales (§25 CLAUDE_RULES.md)

- `Importe = Cantidad × PrecioUnitario` — calculado en `DetalleLineaEditable.Importe`, persistido por el servicio
- `Subtotal = SUM(Detalles.Importe)` — runtime en ViewModel, persistido al `GuardarAsync`
- `Total = Subtotal` — V1 sin IVA independiente
- `ExistenciaEmpresa = SUM(existencias WHERE ProductoId = X)` — runtime al abrir diálogo, **no reserva**

### Validaciones básicas (§7 requerimiento)

- `ClienteId` obligatorio (no puede guardar sin cliente del catálogo)
- Al menos una línea de detalle para documentos nuevos
- `Cantidad > 0` validado en diálogo
- `ProductoId` obligatorio en cada línea — no texto libre

### Observabilidad enriquecida

`ReportarContexto()` ahora incluye el cliente activo en `CategoriaFiltro`:
- `"NUEVA — Cliente: Nombre (ID=X)"` para cotizaciones nuevas
- `"#{id} — Cliente: Nombre (ID=X)"` para cotizaciones existentes

### Grid de detalles actualizado

El `ListView` de detalles ahora muestra 7 columnas:
`[8px] | SKU | Descripción | Existencia | Cantidad | Precio Unitario | Importe`

La columna **Existencia** usa `Foreground=TextFillColorSecondaryBrush` para indicar visualmente que es informativa.

---



### Páginas de documento (workspace tabs)

| Página | Workspace key | Abre desde |
|---|---|---|
| `CotizacionDocumentoPage` | `cotizacion-{id}` / `cotizacion-nueva-{uuid}` | CotizacionesPage (Nuevo, doble clic) |
| `PedidoDocumentoPage` | `pedido-{id}` / `pedido-nuevo-{uuid}` | PedidosPage (Nuevo, doble clic) |
| `OrdenTrabajoDocumentoPage` | `ot-{id}` / `ot-nueva-{uuid}` | OrdenesTrabajoPage (Nuevo, doble clic) |

### Nuevas operaciones de servicio

| Servicio | Método nuevo | Propósito |
|---|---|---|
| `ICotizacionService` | `ActualizarAsync` | Editar encabezado de cotización |
| `ICotizacionService` | `AgregarDetalleAsync` | Agregar línea (persistido inmediato) |
| `ICotizacionService` | `EliminarDetalleAsync` | Quitar línea (persistido inmediato) |
| `ICotizacionService` | `ConvertirAPedidoAsync` | Cotización Aprobada → nuevo Pedido |
| `IPedidoService` | `ActualizarAsync` | Editar encabezado de pedido |
| `IPedidoService` | `AgregarDetalleAsync` | Agregar línea |
| `IPedidoService` | `EliminarDetalleAsync` | Quitar línea |
| `IPedidoService` | `GenerarOrdenTrabajoAsync` | Pedido → nueva OT |
| `IOrdenTrabajoService` | `ActualizarAsync` | Editar encabezado de OT |

### Fórmulas adicionales documentadas (§25 CLAUDE_RULES.md)

Todas las fórmulas de detalles aplican en los documentos:
- `Importe = Cantidad × PrecioUnitario` — calculado en VM y en service al persistir
- `Cotizacion.Subtotal = SUM(detalles.Importe)` — recalculado y persistido en `AgregarDetalle`/`EliminarDetalle`
- `Pedido.Total = SUM(detalles.Importe)` — recalculado al modificar detalles
- `EsUrgente` (OT) = `FechaCompromiso ≤ hoy+1 AND estatus activo` — runtime, no persiste

### Workflows rápidos disponibles

```
Cotización Aprobada → [Convertir a Pedido] → abre Pedido en workspace
Pedido Confirmado/EnProceso → [Generar OT] → abre OT en workspace
OT Nueva → [Avanzar Estado] → EnProceso → Terminada → Entregada
```

---

## Limitaciones V1 / Pendientes

| Item | Estado |
|---|---|
| ~~Crear cotizaciones desde UI~~ | ✅ Implementado — `CotizacionDocumentoPage` |
| ~~Crear pedidos desde UI~~ | ✅ Implementado — `PedidoDocumentoPage` |
| ~~WorkspaceService para documentos~~ | ✅ Implementado — doble clic / botón "Abrir" |
| ~~Conversión Cotizacion → Pedido~~ | ✅ Implementado — `ConvertirAPedidoAsync` |
| ~~Generar OT desde Pedido~~ | ✅ Implementado — `GenerarOrdenTrabajoAsync` |
| ~~Generar Venta desde Pedido~~ | ✅ Implementado — `GenerarVentaAsync` / `IVentaDocumentalService.GenerarDesdePedidoAsync` |
| ~~Marcar OT Entregada~~ | ✅ Implementado — `MarcarEntregadaAsync` (solo desde Terminada) |
| ~~Navegación cruzada Venta → Pedido~~ | ✅ Implementado — botón "Ver Pedido Origen" en `VentaDocumentoPage` |
| SaldoCliente en tiempo real | Pendiente V1.1 — requiere FK ClienteId en CxC |
| Integración OT → descuento inventario | V1.1 |
| `DatePicker` nullable wrapping mejorado | V1.1 — actualmente wrapper properties manuales |

---

## Workflow Actions Layer (implementado 2026-05-09)

### Acciones implementadas

| Acción | Documento Origen | Documento Destino | Validación | Apertura Workspace |
|---|---|---|---|---|
| Convertir a Pedido | Cotización (Aprobada) | Pedido | `Estatus == Aprobada` | ✅ Auto-abre tab Pedido |
| Generar Venta | Pedido (no Cancelado) | Venta Documental | `Estatus != Cancelado` | ✅ Auto-abre tab Venta |
| Generar OT | Pedido (Confirmado/EnProceso) | Orden de Trabajo | `Estatus in {Confirmado, EnProceso}` | ✅ Auto-abre tab OT |
| Marcar Entregada | OT (Terminada) | — (actualiza estatus) | `Estatus == Terminada` | No aplica |
| Ver Pedido Origen | Venta (con PedidoId) | Pedido origen | `PedidoId != null` | ✅ Abre tab Pedido origen |

### Relaciones documentales

```
Cotización #N  ──[ConvertirAPedido]──→  Pedido #M (CotizacionId = N)
Pedido #M      ──[GenerarVenta]───────→  Venta #P  (PedidoId = M)
Pedido #M      ──[GenerarOT]──────────→  OT #Q    (PedidoId = M)
OT #Q          ──[MarcarEntregada]────→  OT #Q    (Estatus = Entregada)
Venta #P       ──[VerPedidoOrigen]────→  navega a Pedido #M
```

### Patrones de implementación

- **Callback Action**: cada ViewModel expone `Action<T>? NotificarXxxxGenerado` — la Page lo asigna para abrir el workspace tab.
- **WorkspaceService.OpenTab**: clave `{tipo}-{id}` evita duplicados; reutiliza tab si ya existe.
- **Validación mínima PYME**: solo se bloquea lo crítico (cancelados no generan documentos destino; Terminada requerida para Entregar).
- **Sin BPM**: no hay motor de workflow; las reglas son `if` simples en los commands.
| Editar líneas existentes de cotizacion/pedido | V1.1 — actualmente solo agregar/quitar |
