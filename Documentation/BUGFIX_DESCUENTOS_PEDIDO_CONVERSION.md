# BUGFIX — Descuentos no se preservaban al convertir Cotización → Pedido

**Fecha**: 2026-05-15  
**Severidad**: CRÍTICA  
**Tiempo de resolución**: sesión extensa (múltiples iteraciones)  
**Estado**: ✅ RESUELTO

---

## Síntoma observable

Al convertir una Cotización con descuentos a un Pedido:
- `PedidoDetalle.DescuentoPct = 0.00` (siempre, aunque la cotización tuviera 5%, 20%, 70%)
- `PedidoDetalle.Importe = PrecioUnitario × Cantidad` (sin descuento aplicado)
- `Pedido.Total` incorrecto (mayor al real)
- `Pedido.Subtotal` correcto (indicio clave de que la conversión SÍ leía los datos bien)
- `FechaModificacion` del Pedido era 25-53 segundos posterior a `FechaCreacion`

---

## Causa raíz — cadena de 3 capas

### Capa 1: EF Core change tracker con datos stale

**Qué pasaba**: `ConvertirAPedidoAsync` cargaba la Cotización con `.Include(x => x.Detalles)` usando tracking normal. El `ErpDbContext` es Scoped y vive toda la sesión de edición. Si el usuario aplicó descuentos (mediante `ActualizarDescuentoAsync` → delete+readd de cada línea), el identity map del contexto podía tener versiones antiguas de los `CotizacionDetalle` (las previas al descuento, marcadas como `Deleted`). Al hacer `Include`, EF podía retornar valores del identity map en lugar de leer fresh del DB.

**Evidencia**: `Pedido.Subtotal = 11071.10` (correcto, calculado desde `detalles.Sum`) mientras `Pedido.Total = 12339` (calculado desde `PrecioUnitario` — los valores stale).

**Fix aplicado**:
```csharp
// ANTES (con tracking — puede retornar valores stale del identity map)
var c = await _context.Cotizaciones
    .Include(x => x.Detalles)
    .Include(x => x.Cargos)
    .FirstOrDefaultAsync(...);

// DESPUÉS (AsNoTracking para detalles/cargos — siempre lee fresco del DB)
var c = await _context.Cotizaciones.FirstOrDefaultAsync(...);  // tracked para update de Estatus
var detallesDb = await _context.CotizacionesDetalle.AsNoTracking()
    .Where(d => d.CotizacionId == cotizacionId).ToListAsync(ct);
var cargosDb = await _context.CotizacionesCargos.AsNoTracking()
    .Where(cc => cc.CotizacionId == cotizacionId).ToListAsync(ct);
```

También: calcular `Subtotal` y `Total` del Pedido desde los valores frescos, no desde `c.Subtotal/Total`:
```csharp
Subtotal = detalles.Sum(d => d.Importe),
Total    = detalles.Sum(d => d.Importe) + cargos.Sum(cc => cc.Importe),
```

---

### Capa 2: `PedidoService.AgregarDetalleAsync` sin CommercialDocumentCalculator

**Qué pasaba**: `AgregarDetalleAsync` calculaba `Importe = Cantidad × PrecioUnitario` sin aplicar descuento y no copiaba `DescuentoPct`/`IvaAplicable` del DTO. Esto afectaba tanto la creación manual de pedidos como el path delete+readd de `ActualizarCantidadAsync`.

**Fix aplicado**:
```csharp
// ANTES
Importe = dto.Cantidad * dto.PrecioUnitario  // sin descuento, sin DescuentoPct

// DESPUÉS
DescuentoPct = dto.DescuentoPct,
IvaAplicable = dto.IvaAplicable,
Importe      = CommercialDocumentCalculator.CalcularImporteLinea(
                   dto.Cantidad, dto.PrecioUnitario, dto.DescuentoPct)
```

---

### Capa 3: WinUI 3 DataTemplate — `ValueChanged` durante render inicial

**Qué pasaba** (la más difícil de detectar): cuando `PedidoDocumentoPage` se abría en Workspace Tab y el DataTemplate renderizaba los `NumberBox` de Cantidad, WinUI 3 disparaba `ValueChanged` como parte del binding inicial. Esto llamaba a `ActualizarCantidadAsync` que ejecutaba **delete + readd** del detalle INCLUSO cuando la cantidad no había cambiado.

En ese momento, si `AgregarDetalleAsync` no usaba `CommercialDocumentCalculator` (Capa 2), el resultado era `Importe = PrecioUnitario` sin descuento.

**Evidencia**: `FechaModificacion` del Pedido era 25-53 segundos después de `FechaCreacion` — justo el tiempo que tardaba el usuario en ver el documento después de la conversión.

**Intentos fallidos**:
- `if (double.IsNaN(args.OldValue)) return;` — WinUI 3 no garantiza `OldValue=NaN`; puede ser `0` u otro valor
- `if (nuevaCantidad == linea.Cantidad) return;` — en ViewModel — no siempre previene el disparo
- `Mode=OneWay` en el NumberBox de descuento — ayuda pero no previene el de Cantidad

**Fix definitivo — `Page.Loaded` guard**:
```csharp
// En el constructor, ANTES de InitializeComponent():
_listaParaEdicion = false;
Loaded += (_, _) => _listaParaEdicion = true;

// En TODOS los handlers que modifican BD:
private async void NumberBox_Cantidad_ValueChanged(...)
{
    if (!_listaParaEdicion) return;  // ← bloquea inicialización
    ...
}
```

`Page.Loaded` es el único evento en WinUI 3 que garantiza que el árbol visual está completamente renderizado y todos los bindings iniciales han disparado. Cualquier `ValueChanged` **antes** de `Loaded` es inicialización — no acción del usuario.

---

## EF Core — HasDefaultValue gotcha (relacionado)

**Descubierto durante el diagnóstico**: `HasDefaultValue(0m)` en las configuraciones EF de `PedidoDetalle` y `CotizacionDetalle` causaba que EF Core usara `ValueGenerated.OnAdd`, tratando el valor `0m` como "sentinel" y potencialmente omitiéndolo del INSERT.

**Fix**: Eliminar `HasDefaultValue` de ambas configuraciones. El DB ya tiene el `DEFAULT 0` del script SQL. Sin `HasDefaultValue`, EF usa `ValueGenerated.Never` implícito y siempre incluye el campo en INSERT/UPDATE.

```csharp
// ANTES
builder.Property(e => e.DescuentoPct).IsRequired()
    .HasColumnType("decimal(5,2)").HasDefaultValue(0m);  // ← problema

// DESPUÉS  
builder.Property(e => e.DescuentoPct).IsRequired()
    .HasColumnType("decimal(5,2)");  // EF usa ValueGenerated.Never implícito
```

---

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `CotizacionService.cs` | `AsNoTracking()` para detalles/cargos en conversión; Subtotal/Total calculados desde valores frescos |
| `PedidoService.cs` | `AgregarDetalleAsync`: usa `CommercialDocumentCalculator`; `CrearAsync`: ídem; `MapToDto`: incluye `DescuentoPct`, `IvaAplicable`, `Sku` |
| `PedidoDetalleConfiguration.cs` | Eliminado `HasDefaultValue` |
| `CotizacionDetalleConfiguration.cs` | Eliminado `HasDefaultValue` |
| `PedidoDocumentoPage.xaml.cs` | `_listaParaEdicion` guard en todos los NumberBox handlers |

---

## Corrección de datos en BD

Se ejecutó un script SQL para corregir todos los Pedidos existentes que tenían `DescuentoPct=0` incorrectamente:

```sql
UPDATE pd
SET pd.DescuentoPct = cd.DescuentoPct,
    pd.IvaAplicable = cd.IvaAplicable,
    pd.Importe      = ROUND(pd.Cantidad * pd.PrecioUnitario * (1 - cd.DescuentoPct / 100.0), 2)
FROM ventas.PedidoDetalle pd
JOIN ventas.Pedido p ON pd.PedidoId = p.Id
JOIN ventas.CotizacionDetalle cd 
    ON cd.CotizacionId = p.CotizacionId AND cd.ProductoId = pd.ProductoId
WHERE p.CotizacionId IS NOT NULL
  AND ABS(pd.DescuentoPct - cd.DescuentoPct) > 0.001;
```

---

## Mejoras potenciales futuras

### 1. Auditoría automática de integridad comercial
Agregar validador en `WorkflowAuditService` que detecte pedidos con `Total ≠ SUM(detalles.Importe)` como señal temprana de este tipo de bug.

### 2. Snapshot documental explícito (mejora arquitectónica)
Actualmente el Pedido hereda los datos de la Cotización vía FK + AsNoTracking. Sería más robusto persistir un snapshot completo en el momento de conversión (similar a cómo `NombreCliente` ya es un snapshot). Considerar agregar columnas `DescuentoPctSnapshot` o una tabla `PedidoSnapshotDetalle`.

### 3. Patrón `Page.Loaded` estándar en CLAUDE_RULES
Documentar en `CLAUDE_RULES.md` la regla: en WinUI 3, **todos** los handlers de `NumberBox.ValueChanged` que persisten datos deben verificar un flag `_listaParaEdicion` activado por `Page.Loaded`. Esto previene la clase de bugs donde el render inicial del DataTemplate dispara operaciones de BD.

### 4. Guard en ViewModel vs en View
El guard `_listaParaEdicion` está en la View (Page). Considerar un patrón más robusto donde el ViewModel también tenga un estado `_inicializado` que se active explícitamente, separando claramente cuándo los cambios son del sistema vs del usuario.

### 5. Tests de integración para conversión
Agregar un test que:
1. Cree una Cotización con descuentos
2. Aplique descuento global
3. Convierta a Pedido
4. Verifique que `PedidoDetalle.DescuentoPct` == `CotizacionDetalle.DescuentoPct`
5. Verifique que `PedidoDetalle.Importe` == `CommercialDocumentCalculator.CalcularImporteLinea(...)`

---

## Lección aprendida

Este bug fue difícil porque era una **cadena de 3 causas independientes** que se enmascaraban entre sí:

- La Capa 1 (stale cache) hacía que los datos llegaran incorrectos a la conversión
- La Capa 2 (AgregarDetalleAsync) hacía que incluso con datos correctos, el delete+readd los corrigiera mal  
- La Capa 3 (render inicial) era el detonante invisible que activaba el delete+readd sin que el usuario hiciera nada

Cada fix individual parecía resolver el problema pero la siguiente capa lo reintroducía. Solo con los tres fixes aplicados simultáneamente quedó estable.
