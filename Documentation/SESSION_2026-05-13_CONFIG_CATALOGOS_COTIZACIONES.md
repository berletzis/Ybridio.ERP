# Sesión 2026-05-13 — Configuración Global + Catálogos + Cotizaciones Comerciales

Build final: ✅ 0 errores | BD: YBRIDIO-26 actualizada

---

## Resumen ejecutivo

Sesión extensa con múltiples implementaciones institucionales:

1. **Módulo Configuración Global** — NavigationView vertical + catálogos operacionales
2. **Singleton Operational Surface Pattern** — EmpresaPage normalizada
3. **Shared Sequence/Folio Pattern** — SerieDocumento + motor de folios
4. **Commercial Tax Pattern** — catálogo fiscal enriquecido + IConfiguracionFiscalService
5. **Cotizaciones comerciales** — TipoProducto + IvaAplicable + OtrosCargos
6. **Correcciones operacionales** — 5 bugs críticos resueltos

---

## 1. Configuración Global — NavigationView vertical

### Nuevas entidades Domain
- `ParametroGlobal` — clave-valor de configuración operacional (`catalogos.ParametroGlobal`)
- `OtroCargo` — cargos accesorios documentales Flete/Maniobras/Seguro (`catalogos.OtroCargo`)

### Nuevos servicios Application
| Servicio | Propósito |
|---|---|
| `IParametroGlobalService` / `ParametroGlobalService` | CRUD + getters tipados (decimal, int, bool, string) |
| `IOtroCargoService` / `OtroCargoService` | CRUD catálogo cargos accesorios |
| `ITipoImpuestoService` / `TipoImpuestoService` | CRUD catálogo fiscal |
| `IUnidadMedidaService` / `UnidadMedidaService` | CRUD unidades de medida |
| `ITipoProductoService` / `TipoProductoService` | CRUD tipos de producto |
| `ISerieDocumentoService` / `SerieDocumentoService` | CRUD series documentales |

### ConfiguracionPage — Rediseño completo
- Modo Global: **NavigationView vertical** (Visual Studio Settings style) reemplaza TabView horizontal
- 10 secciones en 4 grupos: EMPRESA / CONFIGURACIÓN / CATÁLOGOS / SISTEMA
- `SeguridadGlobalPage` extrae los sub-tabs de seguridad del ConfiguracionPage
- Modo Tienda: sin cambios

### Nuevas pages en `Views/Config/`
`ParametrosPage`, `ImpuestosPage`, `OtrosCargosPage`, `UnidadesMedidaPage`, `TiposProductoPage`, `WorkflowPage`, `SeguridadGlobalPage`, `SeriesDocumentoPage`

### Scripts BD ejecutados
- `AddConfigTables_V1.sql` — `catalogos.ParametroGlobal` + `catalogos.OtroCargo`
- `AddSerieDocumento_V1.sql` — `catalogos.SerieDocumento` + columnas `Folio` en 5 tablas
- `UpdateTipoImpuesto_V1.sql` — nuevas columnas en `catalogos.TipoImpuesto`
- `EvolveProductoTipoAndCotizacion_V1.sql` — `TipoProducto.Clave`, `CotizacionDetalle.IvaAplicable`, `ventas.CotizacionCargo`

---

## 2. Singleton Operational Surface Pattern (EmpresaPage)

**Problema:** EmpresaPage con layout aislado (ScrollViewer + StackPanel MaxWidth=600) rompía consistencia visual.

**Solución:** Rediseño completo con:
- CommandBar: Editar / Guardar / Cancelar / Actualizar
- Grid izquierdo (400px): ListView institucional Nombre | RFC | Estado (badge)
- Surface derecho: display read-only o formulario editable según `IsEditing`
- StatusBar con badge "Modo edición activo" (amber)
- Snapshot pattern para cancelar sin perder datos

---

## 3. Shared Sequence/Folio Pattern

### Entidad SerieDocumento
- `TipoDocumentoSerie` enum (10 tipos)
- `SerieDocumento` entity — Prefijo, Longitud, SiguienteNumero, SucursalId
- Tabla `catalogos.SerieDocumento` con índice único EmpresaId+TipoDocumento+SucursalId

### Motor de folios (IFolioGeneratorService)
- Operación **atómica** vía SQL: `UPDATE ... SET SiguienteNumero = SiguienteNumero + 1 OUTPUT DELETED.SiguienteNumero`
- `.ToListAsync()` antes de `.First()` para evitar error "non-composable SQL"
- `IDbContextFactory` para contexto aislado — nunca usa DbContext scoped
- Fallback: retorna `null` si no hay serie configurada (no falla)

### Wiring piloto
- `CotizacionService.CrearAsync` → genera folio automáticamente si hay serie configurada

### Regla: Document Identity Rule
```
COT-000001 → [convertir] → PED-000001 → [convertir] → VTA-000001
```
Cada documento tiene folio propio. NUNCA reutilizar folios en conversiones.

---

## 4. Commercial Tax Pattern (Single Source of Truth Fiscal)

### TipoImpuesto enriquecido
| Campo nuevo | Propósito |
|---|---|
| `Codigo` | Código operacional: IVA16, IVA8, EXENTO |
| `TipoGravamen` | Enum: IVA, IEPS, ISRRetencion, Exento, Otro |
| `EsExento` | Derivado de TipoGravamen==Exento, almacenado para queries |
| `Descripcion` | Descripción técnica/legal |
| `OrdenVisual` | Orden en selectores |

### ParametrosClave (constantes tipadas)
```csharp
ParametrosClave.Fiscal.ImpuestoDefaultProducto  = "impuesto.default.producto"  // TipoImpuestoId (int)
ParametrosClave.Fiscal.ImpuestoDefaultServicio   = "impuesto.default.servicio"
ParametrosClave.Fiscal.ImpuestoDefaultCargo      = "impuesto.default.cargo"
```

### IConfiguracionFiscalService
- Resuelve: `ParametroGlobal` (clave → TipoImpuestoId) → `TipoImpuesto` (datos reales → Tasa)
- Usa `IDbContextFactory` (contexto aislado) para evitar concurrencia
- Fallback: `FiscalConstants.TasaIvaEstandar` si no hay configuración

### CotizacionDocumentoViewModel
- Inyecta `IConfiguracionFiscalService`
- `_tasaIva` cargada via `CargarConfiguracionFiscalAsync()` (fire-and-forget desde Page)
- `RecalcularTotales()` usa `_tasaIva` en lugar de constante hardcoded

### Regla: FiscalConstants es FALLBACK, no fuente primaria
```
TipoImpuesto = QUÉ impuestos existen (catálogo fiscal, fuente única)
ParametroGlobal = CUÁL usar por default (referencia por TipoImpuestoId)
IConfiguracionFiscalService = resuelve la cadena
FiscalConstants = fallback si no hay config
```

---

## 5. TipoProducto + Cotizaciones Comerciales

### TipoProducto — Product Type Classification Pattern
- Campos nuevos: `Clave` (max 10, operacional) + `OrdenVisual`
- **Regla:** Los Servicios son Productos con `TipoProducto.Clave="SERV"` — NO tabla separada
- Índice único `UQ_TipoProducto_EmpresaClave`

### CotizacionDetalle — IvaAplicable persistido
- **Bug fix crítico:** `IvaAplicable` existía solo en memoria y se perdía al recargar
- Ahora persiste en `ventas.CotizacionDetalle.IvaAplicable` (DEFAULT 1)
- `DetalleLineaDto` y `CrearDetalleLineaDto` actualizados
- `CotizacionService` usa el valor real en lugar de hardcoded `false`

### CotizacionCargo — Commercial Charges Pattern
- Nueva entidad `CotizacionCargo` + tabla `ventas.CotizacionCargo`
- Cargos documentales (Flete, Maniobras, Seguro) — NO son productos
- FK CASCADE desde Cotizacion, SetNull desde OtroCargo
- Sección visual propia en CotizacionDocumentoPage
- `RecalcularTotales()`: Total = Subtotal + OtrosCargos + IVA(productos + cargos con IVA)

### Single Document Scroll Pattern
- `CotizacionDocumentoPage.xaml` reestructurado:
  - Row 0 (Auto): CommandBar fijo
  - Row 1 (*): ScrollViewer — ÚNICO dueño del scroll documental
  - Row 2 (Auto): StatusBar fijo
- Todos los ListViews: `ScrollViewer.VerticalScrollBarVisibility="Disabled"` + `ScrollViewer.VerticalScrollMode="Disabled"`
- Sin `MaxHeight` ni `Height="*"` en secciones documentales

---

## 6. Bugs críticos resueltos

### BUG-001 — AutoSuggestBox muestra cadena técnica del record
**Causa:** `ProductoDto` es `sealed record` cuyo `ToString()` devuelve representación técnica.  
**Fix:** Clase wrapper `ProductoSuggestion` con `override string ToString() => $"{Codigo} — {Nombre}"`.

### BUG-002 — DbContext concurrency en ConfiguracionFiscalService
**Causa:** `CargarConfiguracionFiscalAsync()` fire-and-forget corría concurrentemente con `HidratarSelectorClienteAsync()` usando el mismo DbContext scoped.  
**Fix:** `ConfiguracionFiscalService` usa `IDbContextFactory` (contexto aislado por operación). Patrón ADR-026.

### BUG-003 — FolioGeneratorService: "non-composable SQL"
**Causa:** `.FirstAsync()` sobre `SqlQuery<long>($"UPDATE ... OUTPUT ...")` intenta componer SQL sobre sentencia DML.  
**Fix:** `.ToListAsync()` materializa client-side antes de acceder al resultado con `[0]`.

### BUG-004 — OtrosCargos no persisten al guardar documento nuevo
**Causa:** `GuardarAsync` (IsNuevo=true) creaba la cotización pero nunca iteraba `Cargos` para persistirlos via `AgregarCargoAsync`.  
**Fix:** Después de `CrearAsync`, loop explícito sobre `Cargos.ToList()` llamando `AgregarCargoAsync` por cada cargo en memoria.

### BUG-005 — Single Document Session Rule rota cross-host
**Causa dual:**
1. Window key usaba `_cotizacionOriginal?.Id` (readonly, null para docs nuevos aunque guardados). Ahora usa `ViewModel.DocumentoId` (actualizado tras guardar).
2. `_currentInlineDocumentId` no se actualizaba tras primer guardado de doc nuevo. Ahora `DocumentSaved` callback lo actualiza.  
**Fix:** Propiedad `DocumentoId` expuesta en ViewModel. Callback `DocumentSaved` en `BtnNueva_Click`.

---

## Estado final

```
Build:  ✅ 0 errores
BD:     ✅ YBRIDIO-26 — 4 scripts SQL ejecutados
Tests:  N/A (no hay tests unitarios aún)
```

### Pendientes para próxima sesión
- Seed datos iniciales: ejecutar bloque comentado en cada SQL script (TipoImpuestos, SeriesDocumento, TiposProducto)
- Wiring IFolioGeneratorService en PedidoService, VentaService
- Wiring IConfiguracionFiscalService en PedidoDocumentoViewModel
- KI-017/KI-018 — Directorio UX + migración BD socios comerciales
