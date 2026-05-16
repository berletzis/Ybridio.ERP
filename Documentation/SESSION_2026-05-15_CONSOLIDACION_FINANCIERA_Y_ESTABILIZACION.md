# SESSION 2026-05-15 — Consolidación Financiera + Estabilización Y26 + Correcciones Operacionales

## Estado: Build ✅ 0 errores | BD YBRIDIO-26 ✅ actualizada | Branch: master

---

## Sesiones del día (cronológico)

### Sesión 1: OG Standard + Status Badges + Bloqueo Visual Aprobada
- PedidosPage + VentasDocumentalesPage → Operational Grid Standard v2
- Badges EstadoFinanciero + Estado operacional en grids
- Bloqueo visual Cotización Aprobada (PuedeEditar, PuedeEditarLineas, CargoLineaEditable.PuedeEditar)

### Sesión 2: Workflow Operacional Cotizaciones
- `AprobarConGuardadoCommand` — flujo Validar→Guardar→Aprobar
- PuedeEditar restringido a Borrador
- Guards 3 capas para Cotizacion

### Sesión 3: Fix FK ventas→RelacionComercial
- FKs ventas.Venta/OT/Factura/Pedido corregidas a core.RelacionComercial (C-001 Y26)

### Sesión 4: Anticipos, Pagos y Control Financiero
- EstadoFinancieroPedido enum (6 estados incluyendo SobrePagado)
- AnticipoPedido entity + EF config + BD tabla nueva
- RegistrarAnticipoAsync, ListarAnticiposAsync, EstablecerAnticipoRequeridoAsync
- Guard OT condicionado por anticipo (3 capas)
- Venta hereda AnticipoPagado al generar desde Pedido

### Sesión 5: Estabilización Y26 (Post-Auditoría)
- Credenciales externalizadas → appsettings.json + .gitignore institucional
- N+1 Query corregido (PedidoService.ListarAsync: Include vs subquery)
- SucursalId ?? 1 eliminado (error descriptivo en GenerarDesdePedidoAsync)
- SafeFireAndForget helper (Helpers/SafeTaskExtensions.cs)
- Permisos nuevos: anticipo.registrar, anticipo.configurar, venta.cerrar, venta.reabrir, pago.cancelar
- CerrarAsync → permiso venta.cerrar (antes reutilizaba venta.confirmar)
- Guard A-003: ConfirmarAsync valida Detalles vacíos en service

### Sesión 6: Consolidación Ciclo Transaccional
- SobrePagado propagado a converters + texto display
- PedidosPage grid: columna EstadoFinanciero badge
- PuedeEditarCliente: solo Borrador/Autorizado
- PuedeGenerarVenta: incluye Finalizado (genera Venta como documento comercial final)
- VentaDocumentalService: Finalizado PERMITIDO generar Venta, Cancelado BLOQUEADO

### Sesión 7: Correcciones Operacionales
- **Bug crítico selector Pedido**: MostrarDialogoNuevaLinea → AutoSuggestBox con ProductoService (igual a Cotizacion)
- PuedeEditarDescuentoGlobal: solo Borrador
- Breadcrumb redundante eliminado del HeaderStrip
- EstadoFinanciero recalculado en Initialize() — no confiar en valor stale de BD
- BD: 2 pedidos corregidos a SobrePagado=5

### Sesión 8: UX y Layout Bloque Financiero
- CalcularEstadoFinanciero: rounding decimal para evitar falso SobrePagado
- Bloque financiero: ErpOperationalCardStyle sin override de margen (alineación institucional)
- Separador vertical entre resumen y historial de pagos
- "Anticipo mínimo" oculto cuando null
- "Cambio a devolver" visible solo cuando Excedente > 0

### Sesión 9: Header Institucional Pedido
- HeaderStrip siempre visible (no solo en inline mode)
- TituloDocumento = "Pedido PED-000020" → folio institucional
- Padding = ErpDocumentHeaderPadding (igual a CotizacionDocumentoPage)

### Sesión 10: Bug Total con IVA
- **Root cause**: Pedido.Total en BD no incluía IVA → falso SobrePagado de $89.44 = exactamente el IVA
- Fix: RecalcularTotalConIva() helper en PedidoService usando CommercialDocumentCalculator
- AgregarDetalle, EliminarDetalle, AgregarCargo, EliminarCargo → usan nuevo helper
- RegistrarAnticipoAsync → recalcula Total en tiempo real, no confía en valor stale de BD
- BD: 4 pedidos actualizados con Total correcto (IVA incluido), 2 corregidos a Liquidado

---

## ADRs generados en esta sesión

### ADR-066 — SafeFireAndForget Institucional
Fire-and-forget debe capturar excepciones. `Task.FireAndForget(onError, logger)` via `ContinueWith(OnlyOnFaulted)`. Propaga error al ViewModel.ErrorMessage. Aplicado a hydration de selector y CargarConfiguracionFiscalAsync.

### ADR-067 — Pedido.Total debe incluir IVA (Total Institucional)
`Pedido.Total` en BD DEBE incluir IVA para consistencia con UI. PedidoService usa `RecalcularTotalConIva()` con `CommercialDocumentCalculator.CalcularImpuestos() + FiscalConstants.TasaIvaEstandar`. Sin esto, comparación con AnticipoPagado genera falso SobrePagado.

### ADR-068 — Configuración Externalizada (appsettings pattern)
Credenciales y configuración sensible viven en `appsettings.Development.json` (gitignored). Plantilla sin credenciales en `appsettings.json` (en repositorio). `App.xaml.cs` usa `ConfigurationBuilder + AddJsonFile + AddEnvironmentVariables`. ErpDbContextFactory usa `ERP_CONNECTION_STRING` env var.

### ADR-069 — Selector Institucional en todos los documentos comerciales
`MostrarDialogoNuevaLinea()` debe usar `AutoSuggestBox` con `IProductoService.BuscarAsync` en TODOS los documentos (Cotizacion, Pedido, y futuros). La implementación en `CotizacionDocumentoPage.xaml.cs` es la referencia. `ProductoSuggestion` wrapper (namespace `Ybridio.WinUI.Views.Ventas`) es accesible desde cualquier Page en ese namespace.

### ADR-070 — Header Folio Institucional en Document Surface
Toda `DocumentPage` debe mostrar el folio documental en un `HeaderStrip` SIEMPRE visible (no solo en inline mode). Formato: `"{Tipo} {Folio}"` ej: `"Pedido PED-000020"`. Padding = `ErpDocumentHeaderPadding`. Badge de estado junto al título. Referencia: `CotizacionDocumentoPage.xaml` Row 0.

---

## Impacto en Auditoría Estructural

### Nuevas columnas BD (en sesiones anteriores del día)
- `ventas.Pedido`: AnticipoRequerido, AnticipoPagado, EstadoFinanciero ← ya aplicadas
- Nueva tabla `ventas.AnticipoPedido` ← ya aplicada

### Corrección de datos BD (sesión 10)
- `ventas.Pedido.Total` recalculado con IVA para todos los pedidos
- `EstadoFinanciero` corregido para Pedidos 26, 27 (Liquidado), 24, 25 (SobrePagado real)

### Nuevos permisos en BD
- `anticipo.registrar`, `anticipo.configurar` (asignados a roles con pedido.editar)
- `venta.cerrar`, `venta.reabrir`, `pago.cancelar` (asignados por granularidad)
- Total permisos: 51 → 56

### Riesgo de falsos positivos en Auditoría
- `WorkflowAuditService`: puede detectar `Pedido.Total sin IVA` — ya corregido en BD y en code
- Sin nuevos falsos positivos esperados

---

## Pendientes para próxima sesión

1. Migración BD `AddBusinessPartnerModel` (KI-018) — Directorio UX
2. Páginas Directorio: Personas, EmpresasComerciales (KI-017)
3. UI para EstablecerAnticipoRequerido inline en PedidoDocumentoPage
4. Migración enum EstatusCotizacion.Enviada legacy (script BD + eliminar CS0618 pragmas)
5. Auditoría periódica automatizada (Session Closure trigger)

---

## Métricas de sesión

| Métrica | Valor |
|---|---|
| Build | ✅ 0 errores |
| Archivos modificados | ~28 |
| Archivos nuevos | ~8 |
| ADRs nuevos (66-70) | 5 |
| Permisos nuevos en BD | 5 |
| Score Y26 anterior | 91% |
| Score Y26 estimado | 94% |
| Architecture Drift | 0 |
