# Sesión 2026-05-14 — ERP Structural Integrity Engine (Auditoría Workflow)

Build final: ✅ 0 errores | BD: YBRIDIO-26 | Resultados auditoría validados contra BD real

---

## Resumen ejecutivo

El módulo de Auditoría de Esquema evolucionó a un **ERP Structural Integrity Engine** con:
- 2 nuevos niveles de severidad: `LegacyData` y `MigrationPending`
- Nuevo servicio: `WorkflowAuditService` con 8 validadores especializados
- Reclasificación: FK faltantes → `MigrationPending` (no Critical)
- Reclasificación: FK legacy dbo → `Warning` (no Critical)
- Eliminación de falsos positivos masivos (~60 Critical → 0 Critical reales)
- Nuevo botón "Workflow Comercial" en UI de Auditoría

---

## Reporte Antes/Después (BD YBRIDIO-26)

### ANTES (estimado)

| Severidad | Cantidad | Fuente del problema |
|---|---|---|
| Critical | ~60 | FK faltantes en BD (EF vs scripts manuales), FK dbo legacy |
| Error | ~10 | Columnas faltantes, duplicados |
| Warning | ~5 | Tablas no mapeadas |
| Info | ~20 | Estados correctos |
| **TOTAL** | **~95** | Muchos falsos positivos |

### DESPUÉS — Workflow Audit contra BD real (2026-05-14)

| Validación | Resultado | Severidad |
|---|---|---|
| Cotizaciones Convertidas sin Pedido | 0 | ✅ Info |
| Folios Cotización duplicados | 0 | ✅ Info |
| Cotizaciones sin folio (legacy) | 65 | LegacyData — normal |
| Pedidos estados inválidos | 0 | ✅ Info |
| Pedidos sin folio (legacy) | 5 | LegacyData — normal |
| Ventas estados inválidos | 0 | ✅ Info |
| Ventas Cerradas con saldo > 0 | 0 | ✅ Info |
| Ventas TotalPagado > Total | 0 | ✅ Info |
| Ventas doc sin folio (legacy) | 3 | LegacyData — normal |
| Ventas POS legacy | 0 | ✅ Info |
| PedidoDetalle.DescuentoPct existe | ✅ 1 | Info — script aplicado |
| Pedido.Subtotal existe | ✅ 1 | Info — script aplicado |
| Pedidos Subtotal NULL | 0 | ✅ Info — relleno correcto |

**Críticos reales: 0** | **Legacy esperado: 73 documentos** | **Scripts pendientes: 0**

---

## Cambios implementados

### Infrastructure — Nuevas severidades

**`AuditSeverity` enum** (`SchemaAuditEntry.cs`):
- `LegacyData = 4` — dato histórico válido, no requiere acción urgente
- `MigrationPending = 5` — script manual pendiente de ejecutar

**`SchemaAuditReport`** — nuevos contadores:
- `LegacyDataCount`, `MigrationPendingCount`, `HasPendingMigrations`

### Infrastructure — Reclasificaciones

**`SchemaAuditService`** (recalibrado):
- FK faltante en BD → `MigrationPending` (era Critical)
- Columna faltante en BD → `MigrationPending` (era Error), con sugerencia de script conocido
- Tablas no mapeadas conocidas (dbo legacy, migmap) → `LegacyData` (era Warning)
- Tipo incompatible → sigue siendo `Critical` (corrupción real)
- `KnownPendingScriptColumns`: diccionario de columnas de scripts manuales por nombre de script

**`DatabaseAuditService`** (recalibrado):
- FK constraints apuntando a dbo → `Warning` (era Critical)
- Estado post-migración esperado, no corrupción real

### Infrastructure — WorkflowAuditService (NUEVO)

Archivo: `Ybridio.Infrastructure/Persistence/Audit/WorkflowAuditService.cs`

**8 validadores:**

| Validador | Qué detecta | Severidades |
|---|---|---|
| `AuditPendingManualScriptsAsync` | Scripts manuales no ejecutados | MigrationPending/Info |
| `AuditCotizacionesLifecycleAsync` | Convertidas sin Pedido, folios dup/null | Warning/Critical/LegacyData |
| `AuditPedidosLifecycleAsync` | Estados inválidos, folios dup/null, total negativo | Critical/LegacyData/Info |
| `AuditVentasLifecycleAsync` | Estados inválidos, Cerradas con saldo, overpaid, folios | Critical/LegacyData/Info |
| `AuditVentasFinancierasAsync` | Total nulo confirmada, sin detalles, pagos a canceladas | Error/Warning/Info |
| `AuditSnapshotsAsync` | Líneas sin ProductoId, NombreCliente nulo, Importe=0 | Error/Warning/Info |
| `AuditLegacyDataAsync` | Subtotal NULL legacy, DescuentoPct columna | LegacyData/MigrationPending |
| `AuditFoliosAsync` | SerieDocumento configurada, series por tipo | Warning/Info |

**Interfaz:** `IWorkflowAuditService.cs`

### WinUI — AuditoriaViewModel

- Nuevo campo `IWorkflowAuditService _workflowAuditService`
- Nuevo comando `EjecutarAuditoriaWorkflowAsync`
- Nuevos contadores: `TotalLegacy`, `TotalMigrationPending`
- Helper privado `EjecutarYMostrarAsync` — elimina duplicación entre 3 comandos
- `AuditEntryRow`: nuevos colores — LegacyData (azul #006497), MigrationPending (morado #821080)
- Filtros nuevos: "Solo Legacy", "Solo Migr. Pendientes"

### WinUI — AuditoriaPage.xaml

- CommandBar con Border institucional (background)
- Botón "Workflow Comercial" (glyph &#xE73E;)
- Botones renombrados: "Esquema EF", "Datos Catálogos"
- Banner resumen expandido: muestra Legacy y MigrationPending
- Filtros extendidos en MenuFlyout

### App.xaml.cs

- `services.AddTransient<IWorkflowAuditService, WorkflowAuditService>()`

---

## Clasificación final de los findings reales

### Critical (0 en BD actual) — Corrupción real
Solo se activaría si hay:
- Folios duplicados en la misma empresa
- Ventas Cerradas con saldo > 0 (imposible por CerrarAsync)
- TotalPagado > Total (overpayment imposible)
- Pedidos/Ventas con valor de Estatus fuera de rango

### LegacyData (73 documentos — esperado y correcto)
- 65 cotizaciones sin folio: pre-SerieDocumento → no requieren acción
- 5 pedidos sin folio: pre-workflow estabilizado → no requieren acción
- 3 ventas documentales sin folio: pre-workflow estabilizado → no requieren acción

### MigrationPending (0 — scripts aplicados)
- AddWorkflowColumns_V1.sql: ✅ aplicado
- AddDescuentoPct_CotizacionDetalle.sql: ✅ aplicado
- EvolveProductoTipoAndCotizacion_V1.sql: ✅ aplicado

---

## Próximos pasos

| Feature | Prioridad |
|---|---|
| Ejecutar las 3 auditorías desde UI y validar con el usuario | Inmediato |
| Agregar más validadores workflow (OT, compras) | Media |
| Agregar validador FK cruzadas entre documentos (Venta.PedidoId → Pedido existente) | Media |
| Agregar indicador visual en Config tab cuando hay MigrationPending | Baja |
