# Ybridio ERP — Master Architecture Rules for Claude Code

> Estas reglas aplican para TODOS los requerimientos futuros del proyecto.  
> Claude Code debe leer y respetar este documento ANTES de implementar cualquier cambio.  
> Estas reglas son **permanentes** y forman parte oficial de la arquitectura del ERP.

---

## 1. Arquitectura General

El proyecto utiliza arquitectura modular por capas:

- Domain
- Application
- Infrastructure
- WinUI

**Regla obligatoria**: RESPETAR completamente la separación de capas.

---

## 2. Responsabilidades por Capa

### Domain
**Contiene**: entidades, reglas de dominio, enums, contratos de dominio, lógica pura de negocio.  
**NO**: EF Core, WinUI, SQL, XAML, dependencias UI.

### Application
**Contiene**: casos de uso, DTOs, interfaces de servicios, autorización, validaciones de aplicación, lógica operacional.  
**NO**: XAML, controles WinUI, lógica visual.

### Infrastructure
**Contiene**: EF Core, DbContext, configuraciones, Identity, repositorios, queries SQL, servicios externos, caches.  
**NO**: lógica visual, XAML, navegación WinUI.

### WinUI
**Contiene**: Views, ViewModels, navegación, bindings, grids, command bars, experiencia de usuario.  
**NO**: lógica de negocio compleja, autorización central, queries SQL directos.

---

## 3. Regla Crítica — NO Rehacer

**Nunca rehacer**:

- Security Foundation
- Runtime Observability
- WorkspaceService
- SessionService
- Navegación principal / Shell
- Arquitectura existente
- Runtime Diagnostic Panel
- DbContext
- Identity

---

## 4. Regla Crítica — NO Modificar de Más

Claude debe:
- Modificar **únicamente lo necesario**
- Mantener compatibilidad existente
- Evitar refactors innecesarios
- Evitar mover archivos innecesariamente
- Evitar renombrar componentes sin necesidad explícita del usuario

---

## 5. Reutilización Obligatoria

**Antes de crear** servicios, DTOs, entidades, helpers o componentes, Claude debe analizar:
- Qué ya existe
- Qué puede reutilizarse
- Qué puede extenderse

**No duplicar funcionalidades existentes.**

---

## 6. Seguridad

La seguridad utiliza: ASP.NET Identity, RBAC, Profiles, Runtime Authorization, Security Scopes.

### Regla crítica

```csharp
// ❌ NUNCA
if (rol == "Admin") { }

// ✅ SIEMPRE
if (await _auth.PuedeAsync(PermisosClave.Venta.Crear, ct)) { }
```

Usar **siempre**: `IErpAuthorizationService`, `ISecurityScopeResolver`, `PermisosClave`, Runtime Security.

**Los permisos SIEMPRE son DATA. NO hardcodear permisos.**

### Doble capa obligatoria
```csharp
// ViewModel (pre-check UX rápida)
if (!await _auth.PuedeAsync(PermisosClave.Entrada.Ver, ct))
{ ErrorMessage = "Sin permiso..."; return; }

// Service (defensa en profundidad)
if (!await _auth.PuedeAsync(PermisosClave.Entrada.Ver, ct))
    return ServiceResult<T>.Fail("Sin permiso...", ErrorCode.Unauthorized);
```

### Invalidar caché tras cambios de roles/permisos
```csharp
await _cache.InvalidateAsync(userId);
```

---

## 7. Multiempresa / Multisucursal / Multialmacén

El ERP es multiempresa, multisucursal y multialmacén.

Toda nueva funcionalidad debe considerar `EmpresaId`, `SucursalId`, `AlmacenId` y Security Scopes cuando aplique.

Los filtros globales del `ErpDbContext` aplican `!Borrado` y `EmpresaId` automáticamente a todas las entidades. **No romper este mecanismo.**

---

## 8. Runtime Observability

El ERP cuenta con: Runtime Diagnostic Panel, Operational Observability, Security Observability.

**No rehacer la observabilidad.**

Toda nueva operación importante debe integrarse cuando tenga sentido con:
- `IOperationalObservabilityService.Report(GridOperationContext)` tras cada carga/refresh
- `ICurrentContextTracker.SetViewModelContext(CurrentOperationalContext)` al activarse
- `ILiveContextReporter` si el ViewModel vive dentro de un TabView
- Nota `"ACCESO DENEGADO — permiso: x.y"` cuando el acceso falla

```csharp
_observability.Report(new GridOperationContext(
    Module: "Módulo", SubModule: "Sub",
    RecordCount: Items.Count, Duration: sw.Elapsed,
    Notes: denied ? [$"ACCESO DENEGADO — permiso: {permiso}"] : [$"scope info"]
));
```

---

## 9. WorkspaceService

- WorkspaceService **NO es** navegación principal.
- WorkspaceService **SÍ es** persistencia de documentos/workspaces.
- **No convertir** todos los módulos en WorkspaceItems.

Usar WorkspaceService **solamente** cuando exista documento persistente, edición persistente o multitarea real.

---

## 10. UI/UX Standards

El ERP utiliza: diseño Outlook 2026, estilo gris enterprise, tabs horizontales, command bars contextuales, grids Outlook-style, iconografía flat, tipografía consistente.

**No inventar nuevo diseño visual. No romper consistencia visual del ERP.**

### Recursos WinUI 3 obligatorios

| ❌ UWP (rompe en WinUI 3) | ✅ WinUI 3 |
|---|---|
| `SystemControlBackgroundChromeLowBrush` | `LayerFillColorDefaultBrush` |
| `SystemControlHighlightAccentBrush` | `AccentFillColorDefaultBrush` |
| `SystemControlForegroundBaseMediumBrush` | `TextFillColorSecondaryBrush` |

### Thickness: siempre 4 argumentos
```csharp
new Thickness(8, 4, 8, 4)  // ✓
new Thickness(8)            // ✗ no existe en WinUI 3
```

### ViewModel antes de InitializeComponent
```csharp
public MyPage()
{
    ViewModel = App.Services.GetRequiredService<MyViewModel>(); // PRIMERO
    InitializeComponent(); // DESPUÉS
}
```

### ContentDialog: siempre con XamlRoot
Los `ContentDialog` requieren `XamlRoot = XamlRoot` desde la Page. Los callbacks `Action<T>?` en ViewModels son el patrón establecido para abrir diálogos desde la Page.

---

## 11. Grid Standards

Todos los grids deben usar: buscador, contador de registros, virtualización, filtros estándar, diseño consistente.

**Filtros temporales estándar**: Hoy · 7 días · 30 días · 90 días · 6 meses · 1 año · Todo.

**Contenedor estándar**:
```xaml
<Border Margin="20,8,20,0" Background="White" BorderBrush="#E5E5E5" BorderThickness="1">
```

---

## 12. Command Bars

Las command bars deben: ser context-aware, reutilizar el estándar ERP, mantener agrupación consistente.

**No crear ribbons complejos innecesarios.**

---

## 13. SQL Server — Sin Migraciones EF

**No usar** migraciones automáticas de EF Core.

**Usar**: SQL directo, PowerShell, `sqlcmd.exe`.

Todo cambio de esquema requiere:
1. Script `.sql` en `scripts/`
2. Entidad en `Ybridio.Domain`
3. `IEntityTypeConfiguration<T>` en `Ybridio.Infrastructure/Persistence/Configurations/`
4. `DbSet` en `ErpDbContext`

**Precisión decimal obligatoria**:
- Montos/precios: `decimal(18,2)` → `HasColumnType("decimal(18,2)")`
- Cantidades/pesos: `decimal(18,6)` → `HasColumnType("decimal(18,6)")`

---

## 14. Naming Standards

Mantener: nomenclaturas en español, coherencia DbContext/SQL/entidades.

**No mezclar inglés y español en nombres de dominio.**

| Elemento | Regla | Ejemplo |
|---|---|---|
| Entidades | PascalCase español | `MovimientoFinanciero` |
| Servicios | `IXxxService` + `XxxService` | `IFinanzasService` |
| DTOs | `sealed record XxxDto` | `MovimientoFinancieroDto` |
| ViewModels | `sealed partial class XxxViewModel` | `GastosViewModel` |
| Métodos async | sufijo `Async` | `ListarAsync`, `CrearAsync` |
| Campos privados | `_camelCase` | `_service`, `_auth` |
| Claves de permisos | `entidad.accion` minúsculas | `finanzas.crear` |

**Lifetimes DI obligatorios**:
- Servicios Application → `Scoped`
- ViewModels WinUI → `Transient`
- UI Services (Session, Workspace) → `Singleton`
- `IPermissionCache` → `Singleton`

---

## 15. Performance

**No degradar**: grids, EF queries, navegación, observabilidad, runtime.

**Reutilizar**: `MemoryPermissionCache` (TTL 10 min), resolvers y servicios existentes.

**Patrón de carga lazy** en TabViews: flags booleanos + `LoadTab()` + `ILiveContextReporter`. No recargar datos al re-seleccionar un tab ya cargado.

---

## 16. MiniERP PYME Philosophy

Este ERP está orientado a **PYMES**.

**No convertirlo en**: SAP, Oracle, ERP financiero corporativo.

**Mantener**: simplicidad, rapidez, claridad operacional.

---

## 17. Finanzas

El módulo financiero es **Finanzas Operativas** (gastos, ingresos, cuentas por cobrar, cuentas por pagar, flujo operativo).

**No implementar**: contabilidad fiscal, SAT, IFRS, pólizas, contabilidad doble compleja.

---

## 18. Documentación Obligatoria

Toda implementación importante debe actualizar:
- `docs/ARCHITECTURE_STATUS.md`
- El documento del módulo correspondiente (o crear uno nuevo en `docs/`)

Documentar: cambios de arquitectura, decisiones importantes, riesgos, integración runtime, nuevos servicios, nuevas entidades, observaciones relevantes.

---

## 19. Build

Toda implementación debe terminar con **Build = 0 errores**.

**No dejar**: warnings graves, código muerto, TODOs críticos sin documentar.

**Verificar siempre**:
```powershell
dotnet build Ybridio.WinUI/Ybridio.WinUI.csproj -p:Platform=x64
```

---

## 20. Session Log

Al finalizar cada sesión de trabajo, actualizar `memory/session_log.md` con:
- Qué se hizo
- Estado actual (build OK, BD actualizada, etc.)
- Próximos pasos pendientes

---

## 21. Validación Final Obligatoria

Antes de cerrar cualquier implementación, Claude debe validar:

- ✔ Arquitectura intacta (4 capas respetadas)
- ✔ Separación de capas respetada
- ✔ Runtime Security intacto (no se rehízo Security Foundation)
- ✔ Runtime Observability intacta (no se rehízo el panel)
- ✔ WorkspaceService intacto
- ✔ Navegación principal intacta (Shell)
- ✔ UI consistente (estándar Outlook 2026)
- ✔ Build = 0 errores
- ✔ Documentación actualizada
- ✔ Fórmulas y cálculos críticos documentados (§25)
- ✔ XML docs en métodos públicos de Application e Infrastructure (§26)
- ✔ Sin números mágicos ni lógica implícita (§27)
- ✔ Decisiones arquitectónicas relevantes registradas en `DECISIONS.md` (§28)

---

## 22. Restricción Final

Si se cumple cualquiera de las siguientes condiciones, **la implementación es incorrecta**:

- Se rompe la arquitectura de capas
- Se rehacen componentes existentes innecesariamente
- Se mueve lógica incorrectamente entre capas
- Se rompe la observabilidad runtime
- Se hardcodean permisos (`if (rol == "Admin")`)
- Se degrada el performance de grids o navegación
- Se rompe la consistencia visual del ERP
- Build termina con errores
- Se introducen fórmulas o cálculos sin documentar (§25)
- Se usan números mágicos o lógica implícita no explicada (§27)
- Métodos críticos de Application/Infrastructure quedan sin XML doc (§26)

---

## 23. Filosofía General del Proyecto

El objetivo es construir un **miniERP PYME moderno, sólido, rápido y mantenible**.

Debe sentirse: enterprise · profesional · desacoplado · observable · seguro · operativo.

**Sin convertirse en** un ERP corporativo gigantesco e inmantenible.

---

---

# Documentation & Business Logic Rules

> Las secciones 24–30 definen los estándares obligatorios de documentación técnica, operacional y de lógica de negocio.  
> Una implementación sin documentación adecuada de su lógica NO está completa.

---

## 24. Documentación de Lógica Operacional

Toda lógica operacional importante **debe documentarse** en el código que la implementa.

Se considera lógica operacional importante:

- Cálculos y derivaciones de valores (saldos, totales, acumulados)
- Reglas de vencimiento y fechas límite
- Condiciones de autorización y flujos de aprobación
- Filtros runtime aplicados (scopes, empresa, sucursal, almacén)
- Reglas de negocio financieras (pagos parciales, liquidación, descuentos)
- Reglas de inventario (stock mínimo, costeo, existencia disponible)
- Movimientos acumulativos (kardex, saldo de caja, balance financiero)
- Validaciones no obvias (unicidad, integridad referencial de negocio)

**La regla de oro**: si un desarrollador nuevo leyera el método y no entendiera por qué se hace lo que se hace, falta documentación.

---

## 25. Estándares de Fórmulas

Toda fórmula de negocio debe documentar explícitamente:

1. **Qué calcula** — descripción funcional del resultado
2. **Fórmula utilizada** — expresión matemática o lógica
3. **Motivo funcional** — por qué el negocio lo calcula así
4. **Motivo arquitectónico** — por qué se tomó esta decisión de diseño
5. **Runtime vs persistido** — si se calcula en memoria o se almacena en BD
6. **Razón de esa decisión** — por qué no se persistió (o por qué sí)

### Fórmulas del dominio actual — referencia obligatoria

| Fórmula | Expresión | Persiste | Razón |
|---|---|---|---|
| `SaldoPendiente` | `MontoOriginal - MontoPagado` | ❌ Runtime | Calculado evita inconsistencias entre pagos parciales |
| `EsVencida` | `FechaVencimiento < hoy AND SaldoPendiente > 0` | ❌ Runtime | Depende de la fecha actual; persistir requeriría job diario |
| `SaldoAcumulado` | `SaldoAnterior + (Cantidad × Signo)` | ✅ BD | Kardex requiere trazabilidad histórica del saldo en cada movimiento |
| `ExistenciaDisponible` | `SUM(entradas) - SUM(salidas)` por producto+almacén | ✅ BD (`Existencia.Cantidad`) | Consultado frecuentemente en POS; recalcular desde kardex sería costoso |
| `SaldoCaja` | `MontoApertura + ingresos - egresos` | ✅ BD (`Caja.Saldo`) | Actualizado en cada MovimientoCaja; auditable por apertura |
| `CostoPromedio` | `(CostoActual × CantidadActual + CostoNuevo × CantidadNueva) / (CantidadActual + CantidadNueva)` | Pendiente | Implementar cuando se active costeo promedio en Inventario |

### Formato de documentación XML para fórmulas

```csharp
/// <summary>
/// Calcula el saldo pendiente de cobro.
/// </summary>
/// <remarks>
/// Fórmula: SaldoPendiente = MontoOriginal - MontoPagado
/// <para>
/// Calculado en runtime (no persistido) para garantizar consistencia
/// inmediata tras cada pago parcial sin riesgo de desincronización.
/// Si se persistiera, cada llamada a <see cref="RegistrarPagoAsync"/>
/// debería actualizar el campo — innecesario con EF tracking.
/// </para>
/// </remarks>
public decimal SaldoPendiente => MontoOriginal - MontoPagado;
```

---

## 26. XML Documentation — Métodos Críticos

Todo método `public` o `internal` en **Domain**, **Application** e **Infrastructure** requiere XML doc.

La obligación es especialmente estricta para:

- Métodos de Application Services (toda la interfaz + implementación)
- Cálculos financieros (fórmulas, razones de diseño)
- Cálculos de inventario (reglas de stock, costeo)
- Métodos con enforcement de seguridad (pre-conditions de permiso/scope)
- Métodos runtime críticos (autorizaciones, resolución de scopes)
- Métodos con side effects no obvios (modifica caché, lanza eventos)

### Qué documentar por tipo de método

| Tipo | `<summary>` mínimo | `<remarks>` recomendado |
|---|---|---|
| Listar/Consultar | Qué filtra, qué permisos valida | Filtros globales activos, scope aplicado |
| Crear/Actualizar | Validaciones que ejecuta, qué retorna | Reglas de unicidad, side effects |
| Fórmula/Cálculo | Fórmula explícita | Runtime vs persistido, razón arquitectónica |
| Autorización | Permiso requerido | Comportamiento si deniega, nivel de evaluación |
| Side effects | Qué estado modifica | Caché invalidado, eventos disparados |

### Reglas de omisión permitidas

- `<returns>` puede omitirse en `void` / `Task` sin valor significativo.
- `<param name="ct">` (CancellationToken) no se documenta — es evidente.
- Métodos privados solo se documentan cuando la lógica es no obvia o implementa un algoritmo.

### Anti-patrón — documentación vacía o redundante

```csharp
// ❌ NO: documenta lo obvio, no agrega valor
/// <summary>Obtiene el producto por ID.</summary>
Task<ProductoDto> ObtenerPorIdAsync(int productoId);

// ✅ SÍ: agrega contexto real
/// <summary>
/// Obtiene un producto por su ID incluyendo categorías, impuesto y unidad de medida.
/// Valida permiso <c>producto.ver</c> antes de retornar datos.
/// </summary>
/// <returns>
/// <see cref="ServiceResult{T}"/> con el <see cref="ProductoDto"/> completo,
/// o <see cref="ErrorCode.NotFound"/> si no existe,
/// o <see cref="ErrorCode.Unauthorized"/> si el usuario no tiene el permiso requerido.
/// </returns>
Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(int productoId, CancellationToken ct = default);
```

---

## 27. Sin Lógica Mágica

**Está prohibido** introducir lógica implícita, números mágicos o reglas de negocio ocultas sin documentación explícita.

### Prohibiciones concretas

```csharp
// ❌ Número mágico — ¿por qué 50? ¿qué representa?
.Take(50)

// ✅ Constante nombrada con razón
private const int MaxResultadosBusqueda = 50; // límite UX: más de 50 resultados no son útiles en búsqueda rápida
.Take(MaxResultadosBusqueda)

// ❌ Condición implícita — ¿qué significa SucursalId == 0?
if (Session.SucursalId != 0)

// ✅ Con contexto documentado
// SucursalId == 0 indica que el usuario no tiene sucursal activa asignada (recién autenticado o empresa sin sucursales)
if (Session.SucursalId != 0)

// ❌ Acumulado sin explicar
existencia.Cantidad -= cantidad;

// ✅ Con comentario de regla de negocio
// Descuenta stock: toda salida reduce la existencia en el almacén origen.
// El movimiento queda registrado en MovimientoInventario para trazabilidad (kardex).
existencia.Cantidad -= cantidad;
```

### Casos que SIEMPRE requieren comentario

- Cualquier valor numérico literal que no sea 0 o 1
- Condiciones de `if` con múltiples operadores combinados
- Comparaciones contra fechas calculadas (ej: `DateTime.Today.AddDays(-30)`)
- Reglas de estado implícitas (`Borrado = false`, `Activo = true` en creación)
- Comportamientos específicos de un provider/framework externo

---

## 28. Documentación de Decisiones Arquitectónicas

Toda decisión arquitectónica relevante debe registrarse en `docs/DECISIONS.md` como un nuevo ADR (Architecture Decision Record).

### Cuándo crear un nuevo ADR

- Se elige un enfoque sobre otro con trade-offs significativos
- Se decide NO implementar algo que podría esperarse (ej: sin migraciones EF, sin contabilidad doble)
- Se introduce un patrón nuevo al proyecto (primera vez que se usa un mecanismo)
- Se cambia una decisión anterior

### Cuándo documentar inline (comentario o XML doc)

Cuando la decisión afecta un método o tipo específico pero no es relevante a nivel global:

```csharp
// Usamos Join() explícito en lugar de navegación Include() porque EF Core
// no puede traducir el método MapToDto() client-side si va dentro de Select().
// Ver patrón establecido en ProductoService.ListarPorEmpresaAsync().
var lista = await query.Include(...).ToListAsync(ct);
return lista.Select(MapToDto).ToList();
```

### Formato mínimo de un ADR en DECISIONS.md

```
## ADR-NNN — Título breve

**Decisión**: qué se decidió hacer.
**Alternativas consideradas**: qué otras opciones existían.
**Razón**: por qué se tomó esta decisión (constraint, performance, simplicidad, negocio).
**Impacto**: qué archivos/patrones se ven afectados.
```

---

## 29. Documentación de Módulos

Todo módulo nuevo o significativamente extendido debe tener su propio documento en `docs/`.

### Módulos con documentación existente

| Módulo | Documento |
|---|---|
| Security Foundation | `docs/SECURITY_FOUNDATION.md` |
| Runtime Enforcement | `docs/RUNTIME_SECURITY_ENFORCEMENT.md` |
| Finanzas Operativas | `docs/FINANZAS_OPERATIVAS.md` |
| Estado de Arquitectura | `docs/ARCHITECTURE_STATUS.md` |
| Decisiones | `docs/DECISIONS.md` |
| Roadmap | `docs/ROADMAP.md` |
| Problemas conocidos | `docs/KNOWN_ISSUES.md` |

### Qué debe contener el documento de un módulo

1. **Objetivo** — qué problema resuelve, para qué tipo de usuario
2. **Qué incluye / qué NO incluye** — alcance intencional del módulo
3. **Estructura de datos** — tablas, entidades, campos clave, relaciones
4. **Fórmulas y cálculos** — todas las fórmulas de negocio del módulo (ver §25)
5. **Permisos** — claves de `PermisosClave.*` utilizadas
6. **Servicios** — interfaces, métodos expuestos, permisos validados
7. **Observabilidad** — cómo se integra con el Runtime Diagnostic Panel
8. **Limitaciones intencionales** — qué no hace y por qué
9. **Roadmap** — próximas mejoras planificadas

### Actualización de ARCHITECTURE_STATUS.md

Cada vez que se implementa o modifica un módulo:
- Actualizar la tabla de módulos con el estado real
- Actualizar el esquema de BD si se agregaron tablas
- Actualizar los servicios listados si se agregaron servicios

---

## 30. Documentación de Métodos con Side Effects o Dependencias Críticas

Los métodos que modifican estado compartido, disparan eventos, invalidan caché o tienen pre-conditions no obvias deben documentar:

### Side effects

```csharp
/// <summary>
/// Registra un pago parcial sobre la cuenta por cobrar indicada.
/// </summary>
/// <remarks>
/// Side effects:
/// - Incrementa <see cref="CuentaPorCobrar.MontoPagado"/> en la BD.
/// - Si MontoPagado alcanza MontoOriginal, el SaldoPendiente queda en 0
///   (la cuenta queda liquidada, pero NO se marca con un flag separado — el saldo
///   es la fuente de verdad).
/// - No invalida caché de permisos (no afecta seguridad).
/// </remarks>
```

### Dependencias de otros servicios o estado

```csharp
/// <summary>
/// Descuenta inventario y registra el movimiento de kardex en una sola transacción.
/// </summary>
/// <remarks>
/// Dependencias:
/// - Requiere que <see cref="Existencia"/> exista para el producto+almacén indicados.
/// - El <see cref="MovimientoInventario"/> se crea con SaldoAcumulado calculado
///   a partir del saldo previo en la misma operación (no recalculado a posteriori).
/// - Usa concurrencia optimista via RowVersion; lanzará <see cref="DbUpdateConcurrencyException"/>
///   si dos operaciones concurrentes intentan modificar la misma existencia.
/// </remarks>
```

### Filtros y scopes aplicados

```csharp
/// <summary>
/// Lista existencias con enforcement de autorización y scope de almacén.
/// </summary>
/// <remarks>
/// Filtros aplicados (en orden):
/// 1. Filtro global ErpDbContext: EmpresaId == session.EmpresaId (automático)
/// 2. Filtro global ErpDbContext: !Borrado (automático)
/// 3. Permiso runtime: existencia.ver via IErpAuthorizationService
/// 4. Scope de almacén: si el usuario tiene almacenes restringidos,
///    solo retorna existencias de esos almacenes específicos.
///    Si la lista está vacía (SuperAdmin o sin restricción), retorna todos.
/// </remarks>
```

---
