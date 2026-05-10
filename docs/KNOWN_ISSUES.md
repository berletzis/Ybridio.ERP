# KNOWN_ISSUES.md — Problemas Conocidos y Limitaciones

> Última actualización: 2026-05-08 (Document Workflow UX + Identity fix)  
> Formato: `[SEVERIDAD] Descripción — Workaround / Plan`

---

## Severidades

- **BLOCKER** — impide operación normal; debe resolverse antes de producción
- **HIGH** — impacto significativo en UX o seguridad; resolver en siguiente iteración
- **MEDIUM** — limitación funcional; resolver en fase 2
- **LOW** — cosmético o técnico menor; backlog

---

## Problemas Activos

### [MITIGADO] KI-013 — DbContext Concurrency en refresh/load commands

**Módulo**: Todos los ViewModels con `LoadAsync` / `RefrescarAsync` en Inventario, Ventas, Finanzas

**Excepción potencial**:
```
System.InvalidOperationException:
'A second operation was started on this context instance before a previous operation completed.'
```

**Causa raíz**: EF Core `DbContext` es scoped pero NO thread-safe. Si un usuario navega rápidamente entre módulos, hace clic repetido en "Refrescar", o si un timer dispara refresh mientras otro está en curso, **dos operaciones async concurrentes pueden intentar usar el mismo contexto scoped**, lo cual está prohibido por EF Core.

**Contextos de riesgo identificados**:
- `OnNavigatedTo` → `await ViewModel.RefrescarAsync()` mientras hay un refresh previo en curso
- Usuario hace clic repetido en botones de comando async
- Multi-tab navigation rápida
- Timers de diagnóstico (aunque `DiagnosticPanelViewModel` no usa DbContext directamente)

**Mitigación aplicada**: Single-flight guard pattern en todos los ViewModels operacionales:

```csharp
private bool _isRefreshing;  // o _isLoading

[RelayCommand]
public async Task RefrescarAsync(CancellationToken ct = default)
{
    if (_isRefreshing) return;  // ← Early return: evita reentrada
    if (Session.EmpresaId == 0) return;

    _isRefreshing = true;
    IsBusy = true;
    try
    {
        // ... service call con DbContext
    }
    finally
    {
        IsBusy = false;
        _isRefreshing = false;  // ← Siempre liberado en finally
    }
}
```

**ViewModels protegidos** (ver `docs/CLAUDE_RULES.md` §7 para lista completa):
- Inventario: `SalidasViewModel`, `EntradasViewModel`, `ExistenciasViewModel`, `KardexViewModel`, `ProductosViewModel`
- Ventas: `CotizacionesViewModel`, `PedidosViewModel`, `OrdenesTrabajoViewModel`
- Finanzas: `GastosViewModel`, `IngresosViewModel`, `CxCViewModel`, `CxPViewModel`

**Impacto del fix**: Los comandos de carga/refresh son ahora **single-flight** — si un refresh está en curso, llamadas adicionales se ignoran silenciosamente hasta que el primero complete. Esto previene la excepción de concurrencia DbContext sin bloquear UI ni introducir locks complejos.

**Estado**: ✅ MITIGADO — guards aplicados, build exitoso, runtime estable. La excepción ya no debería ocurrir en escenarios normales de uso multi-tab o refresh rápido.

**Regla preventiva** (agregada a `CLAUDE_RULES.md` §7):
> Todos los ViewModels con `LoadAsync`/`RefrescarAsync` que usen Application services (que indirectamente usan DbContext scoped) DEBEN aplicar el patrón single-flight guard. Ver ejemplos CORRECTO vs INCORRECTO en `docs/CLAUDE_RULES.md`.

---

### [RESUELTO] KI-012 — `IdentityRole<Guid>` no registrado en ErpDbContext

**Módulo**: `PermisoService` (`Ybridio.Application/Services/Permisos/PermisoService.cs`)

**Excepción en runtime**:
```
System.InvalidOperationException:
'Cannot create a DbSet for 'IdentityRole<Guid>' because this type is not included in the model for the context.'
```

**Causa raíz**: El código usaba `_context.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()` para hacer JOIN con los roles del usuario. Sin embargo, `ErpDbContext` hereda de `IdentityDbContext<ApplicationUser, **ApplicationRole**, Guid>` — registra `ApplicationRole` (el tipo custom enterprise), NO el tipo genérico base `IdentityRole<Guid>`. EF Core no puede crear un `DbSet<T>` para un tipo que no está en el modelo.

**Dónde ocurría** (dos instancias en el mismo archivo):
- `TienePermisoAsync()` — Nivel 3 del evaluador de permisos
- `ObtenerPermisosEfectivosAsync()` — Nivel 3 del resolver de conjunto efectivo

**Fix aplicado**:
```csharp
// ❌ ANTES — causa excepción en runtime
_context.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
    .Where(r => rolesUsuario.Contains(r.Name!))

// ✅ DESPUÉS — correcto para ErpDbContext con ApplicationRole
_context.Roles
    .Where(r => rolesUsuario.Contains(r.Name!))
```

**`_context.Roles`** es la propiedad heredada de `IdentityDbContext` que expone `DbSet<ApplicationRole>`, el tipo correcto registrado en el modelo.

**Motivo arquitectónico**: `ErpDbContext` usa Identity custom (`ApplicationRole : IdentityRole<Guid>`) con campos adicionales (`FechaCreacion`, `Borrado`, `RowVersion`). EF Core registra el tipo derivado en el modelo, no el tipo base genérico. Ver ADR-002 en `docs/DECISIONS.md`.

**Impacto del bug**: Toda autorización runtime fallaba con excepción al intentar evaluar permisos heredados por rol (Nivel 3 de evaluación). El login y cualquier operación protegida terminaban en error 500.

**Impacto del fix**: Runtime Security completamente funcional. `TienePermisoAsync` y `ObtenerPermisosEfectivosAsync` operan correctamente. `MemoryPermissionCache` (TTL 10 min) reduce las queries subsecuentes.

**Estado**: ✅ RESUELTO — ambas instancias corregidas, build = 0 errores.

**Regla preventiva** (agregar a CLAUDE_RULES.md §6):
> Nunca usar `_context.Set<IdentityRole<Guid>>()` — siempre usar `_context.Roles` (que expone `DbSet<ApplicationRole>`).

---

### [HIGH] KI-001 — `ListarPorEmpresaAsync` sin guard de autorización en service layer

**Módulo**: ProductoService  
**Descripción**: La firma `Task<IReadOnlyList<ProductoDto>>` no puede retornar `ServiceResult`, por lo que el guard de `producto.ver` solo existe en el ViewModel, no en el servicio.  
**Impacto**: Si `ProductoService.ListarPorEmpresaAsync` se llama desde un contexto diferente (futuro API, otro ViewModel), el permiso no se verifica en el servicio.  
**Workaround**: El ViewModel (`ProductosViewModel.LoadAsync`) hace el pre-check antes de llamar al servicio.  
**Plan**: Cambiar la firma a `ServiceResult<IReadOnlyList<ProductoDto>>` en una iteración futura. Requiere actualizar `PosViewModel` como consumidor secundario.

---

### [HIGH] KI-002 — Servicios de creación de Entradas y Salidas de inventario no implementados

**Módulo**: Inventario  
**Descripción**: `IEntradaService` y `ISalidaService` solo tienen el método `ListarAsync`. No existe `CrearAsync` para documentos de entrada/salida.  
**Impacto**: Los botones "Nuevo" en EntradasPage y SalidasPage son stubs (`[RelayCommand] private void Nuevo() { }`).  
**Workaround**: No hay. Los usuarios no pueden crear entradas/salidas desde la UI.  
**Plan**: Implementar en Fase 1 (ADR ver ROADMAP.md).

---

### [MEDIUM] KI-003 — 128 warnings MVVMTK0045 (AOT compatibility)

**Módulo**: WinUI (todos los ViewModels)  
**Descripción**: CommunityToolkit.Mvvm 8.4.0 genera warning MVVMTK0045 para campos `[ObservableProperty]`. La recomendación es migrar a `partial property` (CommunityToolkit 8.4+).  
**Impacto**: Solo en compilación (warnings). No hay impacto en runtime. El ERP no usa AOT compilation.  
**Workaround**: Ignorar en desarrollo.  
**Plan**: Migrar campos a `partial property` progresivamente. No es urgente.

---

### [MEDIUM] KI-004 — Indicador visual de CxC/CxP vencidas no implementado

**Módulo**: Finanzas  
**Descripción**: El campo `EsVencida` existe en `CxCDto` y `CxPDto`, pero el DataTemplate de CxCPage/CxPPage muestra el valor bool como texto (`True/False`) sin formato visual.  
**Impacto**: Los usuarios no identifican visualmente las cuentas vencidas de un vistazo.  
**Workaround**: Ver la columna "Estado" del grid (muestra True/False).  
**Plan**: Agregar `DataTrigger` o `IValueConverter` para colorear la fila en rojo si `EsVencida = true`. Ver ROADMAP.md Fase 1.3.

---

### [MEDIUM] KI-005 — `ContextoFinanciero.Usuario` sin queries activas

**Módulo**: Finanzas  
**Descripción**: La arquitectura de `MovimientoFinanciero` incluye `ContextoFinanciero.Usuario` y `UsuarioContextoId`, pero las queries en `FinanzasService.ListarAsync` no filtran por usuario. Todo se muestra a nivel empresa.  
**Impacto**: No hay separación entre gastos personales y empresariales en V1.  
**Workaround**: No crear movimientos con `Contexto = Usuario` en V1.  
**Plan**: Activar en Finanzas V2 con una UI separada o un toggle.

---

### [MEDIUM] KI-006 — Seed de permisos usa EmpresaId=1 hardcodeado en categorías

**Módulo**: Finanzas / SQL  
**Descripción**: El script `finanzas_ddl.sql` inserta categorías seed para TODAS las empresas del CROSS JOIN, pero si se crea una empresa nueva después de ejecutar el script, no tendrá categorías seed.  
**Impacto**: Empresas creadas después del deploy inicial tendrán `CategoriaFinanciera` vacío.  
**Workaround**: Ejecutar manualmente el bloque de seed para la empresa nueva, o crear categorías desde la UI (pendiente de implementar).  
**Plan**: Crear trigger o job que inserte categorías seed al crear una empresa nueva.

---

### [MEDIUM] KI-007 — Totales financieros no visibles en statusbar

**Módulo**: Finanzas  
**Descripción**: El statusbar de GastosPage/IngresosPage muestra "N gasto(s)" pero no el total de montos del período filtrado.  
**Impacto**: El usuario no puede ver rápidamente cuánto gastó en el mes.  
**Workaround**: Ver individualmente cada registro.  
**Plan**: Agregar propiedad calculada `TotalPeriodo` en el ViewModel basada en la colección visible. Ver ROADMAP.md Fase 1.3.

---

### [LOW] KI-008 — Cadena de conexión hardcodeada en App.xaml.cs

**Módulo**: Infraestructura  
**Descripción**: La cadena de conexión SQL Server está literal en `App.xaml.cs`, incluyendo usuario y contraseña en texto plano.  
**Impacto**: Solo en dev. No hay impacto funcional. No deben existir datos sensibles de producción ahí.  
**Workaround**: Aceptable en entorno de desarrollo.  
**Plan**: Mover a `appsettings.json` + DPAPI encriptado antes de producción.

---

### [LOW] KI-009 — Fechas en grids sin formato localizado

**Módulo**: Inventario / Finanzas (todos los grids)  
**Descripción**: Los campos `Fecha` en DataTemplates usan `{x:Bind Fecha}` que aplica el formato por defecto de `DateTime.ToString()` (ISO 8601 largo).  
**Impacto**: Las fechas se muestran como `5/8/2026 10:30:00 AM` en lugar de `08/05/2026`.  
**Workaround**: El usuario puede leerlas. No es incorrecto, solo no formateado.  
**Plan**: Agregar `IValueConverter` global `DateConverter` con formato `dd/MM/yyyy`. Aplicar a todos los templates.

---

### [LOW] KI-010 — Páginas de módulo Ventas y Contactos son placeholders

**Módulo**: Ventas, Contactos  
**Descripción**: `VentasPage` y `ContactosPage` existen en el Shell como módulos pero muestran contenido mínimo o placeholder.  
**Impacto**: Los usuarios pueden navegar a esos módulos pero no tienen funcionalidad operativa completa.  
**Workaround**: Usar POS para ventas.  
**Plan**: Implementar en Fase 2 (ROADMAP).

---

### [LOW] KI-011 — Sin paginación en grids con muchos registros

**Módulo**: Todos los módulos con ListView  
**Descripción**: Los grids cargan todos los registros del período filtrado en memoria y los muestran. Si hay >5,000 registros en el período, la carga puede ser lenta.  
**Impacto**: Solo observable con volúmenes grandes de datos.  
**Workaround**: Usar filtros de período cortos (7 días, 30 días).  
**Plan**: Implementar paginación virtual en módulos con historial largo (Kardex, Ventas) en Fase 4.

---

## Limitaciones de Diseño Intencionales

Estas no son bugs sino decisiones arquitectónicas conscientes:

| Limitación | Razón | Referencia |
|---|---|---|
| Sin contabilidad doble | Fuera del alcance PYME | ADR-010 |
| Sin integración SAT/CFDI | Requiere certificados + módulo fiscal | Roadmap Fase 4 |
| Sin multi-moneda | Simplicidad V1 | Roadmap Fase 3 |
| Sin auditoría de cambios de seguridad | TTL caché cubre casos básicos | Roadmap Fase 2 |
| Soft-delete universal (sin papelera de reciclaje) | Filtros globales simples | ADR-013 |
| Tablas de Identity en schema `seguridad.*` | Identity adaptado al ERP | ADR-002 |

---

## Problemas Resueltos (Histórico)

| ID | Descripción | Resuelto en |
|---|---|---|
| Entradas/Salidas con `ObservableCollection<object>` | Tipado a `EntradaResumenDto`/`SalidaResumenDto` | 2026-05-08 |
| ExistenciasPage stub "Próximamente" | Implementada con grid real y scope de almacén | 2026-05-08 |
| `PerfilesPage` mostrando placeholder | Implementada con CRUD completo | 2026-05-08 |
| `Application.Current` ambiguo con `Ybridio.Application` | Alias `XamlApp = Microsoft.UI.Xaml.Application` | 2026-05-08 |
| `ContentDialog.ShowAsync()` sin `using System;` | CS4036: requiere `using System` para WinRT interop | 2026-05-08 |
| **`IdentityRole<Guid>` DbSet exception en PermisoService** | Ver KI-012 abajo — fix aplicado: `_context.Roles` | 2026-05-08 |
| VentasPage stub "Próximamente" | Implementada con TabView lazy-load (4 tabs Sales Core) | 2026-05-08 |
| `DatePicker.Date` tipo `DateTime` incompatible con `DateTimeOffset` | Wrapper properties `FechaOffset`/`FechaCompromisoOffset` | 2026-05-08 |
