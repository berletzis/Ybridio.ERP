# DECISIONS.md — Registro de Decisiones Arquitectónicas

> Este documento registra las decisiones técnicas importantes tomadas durante el desarrollo de Ybridio ERP,
> incluyendo la alternativa descartada y la razón de la elección.  
> Última actualización: 2026-05-08

---

## ADR-001 — Clean Architecture en 4 capas

**Decisión**: Domain → Infrastructure → Application → WinUI

**Alternativas consideradas**:
- Arquitectura en 2 capas (UI + acceso directo a datos)
- CQRS con MediatR

**Razón**:
El negocio requiere lógica reutilizable entre POS, módulo administrativo y futuras apps móviles/web. La separación en 4 capas permite cambiar la presentación sin tocar lógica de negocio. CQRS fue descartado por overhead innecesario en un ERP PYME.

**Impacto**: Todos los servicios viven en Application, los ViewModels son orquestradores, la BD solo es alcanzable desde Infrastructure.

---

## ADR-002 — ASP.NET Core Identity adaptado (no reemplazado)

**Decisión**: Extender `IdentityUser<Guid>` y `IdentityRole<Guid>` con propiedades de negocio (EmpresaId, Nombre, Activo, Borrado).

**Alternativas consideradas**:
- Sistema de autenticación custom desde cero
- OpenIddict / Duende IdentityServer

**Razón**:
Identity ya resuelve hash de contraseñas, claims, tokens y roles. Reimplementarlo es riesgo de seguridad. OpenIddict agrega complejidad innecesaria para un desktop app. La extensión mantiene compatibilidad y agrega las propiedades de negocio.

**Impacto**: `ApplicationUser` y `ApplicationRole` en `seguridad.*` schema. ErpDbContext hereda de IdentityDbContext.

---

## ADR-003 — Un solo DbContext, filtros globales automáticos

**Decisión**: `ErpDbContext` único con filtros de soft-delete y multi-tenancy aplicados via reflection en `OnModelCreating`.

**Alternativas consideradas**:
- DbContext separado por módulo
- Filtros manuales en cada query

**Razón**:
Múltiples DbContexts generan problemas de transacciones cruzadas (ej: Venta → descuenta inventario → registra movimiento de caja). Los filtros globales eliminan el riesgo de olvidar `!Borrado` o el `EmpresaId` en cualquier query.

**Impacto**: Toda entidad con `Borrado` y `EmpresaId` queda automáticamente filtrada. El bypass (`EmpresaId == 0` para tooling) está documentado.

---

## ADR-004 — ServiceResult<T> sin excepciones de negocio

**Decisión**: Todos los métodos de escritura retornan `ServiceResult<T>` o `ServiceResult`. Las excepciones solo son para errores de infraestructura inesperados.

**Alternativas consideradas**:
- Lanzar excepciones de dominio (DomainException)
- Fluent Validation con throw

**Razón**:
Las excepciones tienen costo de performance y hacen el flujo difícil de seguir en la UI. `ServiceResult` permite que el ViewModel decida qué mostrar al usuario sin parsear mensajes de error. El `ErrorCode` enum permite reacciones específicas (e.g., `Unauthorized` → mostrar mensaje diferente que `NotFound`).

**Impacto**: Patrón obligatorio en todos los servicios. ViewModels checan `result.Success` antes de actuar.

---

## ADR-005 — RBAC + Profiles con evaluación en 3 niveles

**Decisión**: Los permisos se resuelven en orden: UsuarioPermiso (override) → PerfilPermiso → RolPermiso. Un denegado explícito veta todos los niveles.

**Alternativas consideradas**:
- Solo roles (sin perfiles ni overrides)
- Permisos flat por usuario (sin herencia)

**Razón**:
Solo roles limita la flexibilidad cuando un usuario necesita permisos extra sin cambiar su rol. Los perfiles reutilizables resuelven asignaciones frecuentes (ej: "POS Básico"). Los overrides permiten excepciones puntuales sin crear roles ad-hoc.

**Impacto**: `PermisoService` evalúa los 3 niveles. `MemoryPermissionCache` (TTL 10 min) evita N+1 queries en cada `PuedeAsync`.

---

## ADR-006 — PermisosClave como constantes tipadas (no enum)

**Decisión**: Clase estática `PermisosClave` con subclases por módulo y constantes `string`.

**Alternativas consideradas**:
- Enum de permisos
- Strings directo en código

**Razón**:
Los enums no se serializan bien a `string` sin conversores y complican la seed de BD. Los strings literales generan typos silenciosos. Las constantes tipadas dan autocompletado, son refactorizables y coinciden exactamente con los valores en BD (`entidad.accion` minúsculas).

**Impacto**: Todo `PuedeAsync(...)` debe usar `PermisosClave.*`. La regla es obligatoria y verificable con grep.

---

## ADR-007 — SQL DDL directo (sin migraciones EF)

**Decisión**: Los cambios de esquema se aplican con scripts `.sql` ejecutados via `sqlcmd.exe`. EF Core solo se usa como ORM, no como herramienta de migración.

**Alternativas consideradas**:
- EF Core Migrations
- DbUp / Flyway

**Razón**:
Las migraciones EF generan archivos difíciles de auditar y no funcionan bien con esquemas SQL Server con múltiples schemas nombrados. Los scripts SQL son legibles, versionables en git y permiten control total del DDL. El cliente tiene acceso directo al SQL Server y prefiere este approach.

**Impacto**: Todo cambio de esquema requiere: (1) script SQL en `scripts/`, (2) entidad Domain, (3) EF Configuration, (4) DbSet en ErpDbContext.

---

## ADR-008 — WorkspaceService: dos capas de contenido en el Shell

**Decisión**: `ShellPage` tiene dos capas: `ModuleFrame` (módulo principal, siempre renderizado) y `WorkspaceTabView` (tabs persistentes, superpuesto).

**Alternativas consideradas**:
- Un solo Frame con navegación de pila
- Tabs solo para el workspace, sin ModuleFrame

**Razón**:
El ModuleFrame permite que módulos como Dashboard, POS e Inventario tengan su propio TabView interno sin conflictos. El WorkspaceService agrega tabs de trabajo (Productos específicos, comparaciones) que persisten al cambiar de módulo, similar a VS Code.

**Impacto**: Los módulos que viven en ModuleFrame tienen un Frame propio. Los ítems de workspace tienen su propio ciclo de vida.

---

## ADR-009 — Lazy-loading de tabs en páginas de módulo

**Decisión**: Las páginas con TabView (InventarioPage, FinanzasPage, ConfiguracionPage) cargan cada tab en su Frame solo la primera vez que se activa.

**Alternativas consideradas**:
- Cargar todos los tabs al navegar al módulo
- Recargar el tab en cada selección

**Razón**:
Cargar todo al inicio degrada el tiempo de navegación inicial. Recargar en cada selección destruye el estado (búsqueda activa, ítem seleccionado). La carga lazy con flags booleanos (`_entradasLoaded`) mantiene el estado y carga bajo demanda.

**Impacto**: Patrón `ILiveContextReporter` para que tabs ya cargados actualicen el contexto de observabilidad sin recargar datos.

---

## ADR-010 — Finanzas Operativas: NO contabilidad doble

**Decisión**: El módulo Finanzas registra movimientos simples (gasto/ingreso) sin contrapartidas contables.

**Alternativas consideradas**:
- Sistema contable con partida doble
- Pólizas contables con catálogo de cuentas

**Razón**:
El target es PYME comercial que necesita control del flujo de efectivo operativo, no cumplimiento contable formal. La contabilidad doble agrega complejidad (plan de cuentas, pólizas, balanzas, SAT) que está fuera del alcance del producto. Si el cliente necesita contabilidad formal, usará un ERP dedicado (SAP, CONTPAQi) o solicitará la integración explícitamente.

**Impacto**: `MovimientoFinanciero` tiene Concepto, Monto, Fecha, Categoría. No hay Must-Have de contrapartida. `ContextoFinanciero` permite future-proof para finanzas personales.

---

## ADR-011 — Doble capa de enforcement de autorización

**Decisión**: El permiso se verifica en el ViewModel (pre-check UX) Y en el Service (defensa en profundidad).

**Alternativas consideradas**:
- Solo en el Service
- Solo en el ViewModel

**Razón**:
Solo en el Service: el usuario espera el resultado de la query antes de ver el mensaje de error. Solo en el ViewModel: si el Service se llama desde otro punto (futuro API, otra VM), la validación se saltaría. La doble capa garantiza UX rápida y seguridad real.

**Impacto**: Todos los ViewModels con datos sensibles tienen el pre-check. Todos los servicios con write ops tienen el guard. El patrón está en CLAUDE_RULES.md como obligatorio.

---

## ADR-012 — ContentDialog para CRUD en lugar de ventanas separadas

**Decisión**: Los formularios de creación/edición simples usan `ContentDialog` inline en la Page. Solo los formularios complejos (UsuarioDetailWindow) usan ventanas separadas.

**Alternativas consideradas**:
- Ventana secundaria para todos los formularios
- Panel lateral (flyout/panel) inline

**Razón**:
ContentDialog es el patrón WinUI estándar para formularios breves. Las ventanas separadas tienen más overhead (registro, gestión de lifecycle). Los formularios de Gastos, Ingresos, CxC/CxP tienen 4-6 campos — caben cómodamente en un ContentDialog.

**Impacto**: ContentDialogs requieren `XamlRoot` de la Page → los ViewModels exponen callbacks `Action<T>?` en lugar de crear los diálogos directamente.

---

## ADR-013 — Soft-delete universal (sin eliminación física)

**Decisión**: Ninguna entidad se elimina físicamente. Se marca `Borrado = true` y el filtro global la excluye automáticamente.

**Alternativas consideradas**:
- Eliminación física con archive table
- Columna `DeletedAt` datetime (en lugar de bool)

**Razón**:
La eliminación física rompe integridad referencial y destruye historial de auditoría. El bool `Borrado` con filtro global es simple, eficiente y reversible. Un `datetime` añade info útil pero el `FechaModificacion + UsuarioModificacionId` ya captura cuándo y quién borró.

**Impacto**: El filtro global `!Borrado` aplica a todas las entidades de tipo `AuditableEntity` y `CreationAuditEntity`. Las queries administrativas que necesitan ver borrados deben usar `.IgnoreQueryFilters()`.
