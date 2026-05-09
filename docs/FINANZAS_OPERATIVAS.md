# Finanzas Operativas PYME

> Implementado: 2026-05-08

## Objetivo

Módulo de control financiero operacional simple para PYMES. **NO es contabilidad formal** — no incluye pólizas contables, diario general, IFRS, SAT, ni costeo avanzado. Es un sistema rápido, claro y útil para controlar gastos, ingresos, cobros y pagos del día a día.

---

## Qué incluye (V1)

| Sub-módulo | Descripción |
|---|---|
| **Gastos** | Registro de egresos operativos: agua, luz, gasolina, nómina, mantenimiento |
| **Ingresos** | Ingresos no provenientes de ventas: préstamo, recuperación, depósito |
| **Cuentas por Cobrar** | Seguimiento de deudores con saldo pendiente y vencimiento |
| **Cuentas por Pagar** | Seguimiento de acreedores con saldo pendiente y vencimiento |

---

## Acceso

**Configuración Global → Finanzas** no. El módulo es accesible desde el **sidebar principal** mediante el botón "Finanzas" (ícono &#xE8C7;), entre Contactos y el espaciador inferior.

Ruta de navegación: `ShellPage → ModuleFrame → FinanzasPage (TabView con 4 tabs)`

---

## Estructura de Datos

### Schema: `finanzas` (SQL Server)

```
finanzas.CategoriaFinanciera   — catálogo de categorías (Gasto | Ingreso | Ambos)
finanzas.MovimientoFinanciero  — gastos e ingresos (discriminado por Tipo: 1=Gasto, 2=Ingreso)
finanzas.CuentaPorCobrar       — CxC con MontoPagado + FechaVencimiento
finanzas.CuentaPorPagar        — CxP con MontoPagado + FechaVencimiento
```

### Entidades Domain (`Ybridio.Domain.Finanzas`)

- `ContextoFinanciero` (enum): Empresa=0, Sucursal=1, Usuario=2, Familia=3
- `TipoMovimientoFinanciero` (enum): Gasto=1, Ingreso=2
- `CategoriaFinanciera` — hereda `AuditableEntity`
- `MovimientoFinanciero` — hereda `AuditableEntity`; Tipo discrimina Gasto/Ingreso
- `CuentaPorCobrar` — hereda `AuditableEntity`; SaldoPendiente = MontoOriginal - MontoPagado (calculado)
- `CuentaPorPagar` — hereda `AuditableEntity`; ídem

### Filtros globales automáticos (ErpDbContext)
- Soft-delete (`!Borrado`) → aplicado a todos por ser `AuditableEntity`
- Multi-tenancy (`EmpresaId == session.EmpresaId`) → aplicado automáticamente

---

## Permisos

Nuevas claves en `PermisosClave` (módulos: `finanzas`, `cxc`, `cxp` en seguridad.Modulo):

| Clave | Uso |
|---|---|
| `finanzas.ver` | Ver gastos e ingresos |
| `finanzas.crear` | Crear gastos/ingresos |
| `finanzas.editar` | Editar gastos/ingresos |
| `finanzas.eliminar` | Eliminar (soft-delete) gastos/ingresos |
| `cxc.ver` | Ver cuentas por cobrar |
| `cxc.crear` | Crear CxC |
| `cxc.editar` | Editar + registrar pagos CxC |
| `cxp.ver` | Ver cuentas por pagar |
| `cxp.crear` | Crear CxP |
| `cxp.editar` | Editar + registrar pagos CxP |

**Roles con acceso completo**: SuperAdmin, AdministradorEmpresa
**Roles con acceso parcial**: GerenteSucursal (ver + crear)

---

## Servicios Application

| Servicio | Permisos validados | Descripción |
|---|---|---|
| `IFinanzasService` / `FinanzasService` | finanzas.ver/crear/editar/eliminar | CRUD MovimientoFinanciero + ListarCategorías |
| `ICxCService` / `CxCService` | cxc.ver/crear/editar | CRUD CxC + RegistrarPago |
| `ICxPService` / `CxPService` | cxp.ver/crear/editar | CRUD CxP + RegistrarPago |

Todos usan `IErpAuthorizationService.PuedeAsync(PermisosClave.X.Y)` para enforcement runtime (doble capa: ViewModel + Service).

---

## Contexto Financiero

El enum `ContextoFinanciero` permite clasificar movimientos según su pertenencia:

- `Empresa` (default): gastos/ingresos a nivel corporativo, filtrados por EmpresaId del session
- `Sucursal`: gastos/ingresos específicos de una sucursal, con SucursalId opcional
- `Usuario`: finanzas personales del usuario autenticado (arquitectura preparada, no activada en V1)
- `Familia`: uso futuro para grupos familiares

En V1, todos los movimientos usan `ContextoFinanciero.Empresa` o `Sucursal`. La separación por Usuario es arquitectónica y no tiene enforcement en queries todavía.

---

## Observabilidad

Los 4 ViewModels integran `BuildOperationalContext()` y `BuildCurrentContext()` con:
- `Module: "Finanzas"`, `SubModule: "Gastos"/"Ingresos"/"CxC"/"CxP"`
- `EmpresaFilter: Applied`, `SucursalFilter: OmittedExpected` (finanzas operan por empresa)
- `Notes: ["ACCESO DENEGADO — permiso: finanzas.ver"]` cuando el permiso falla

El Runtime Diagnostic Panel (Ctrl+Shift+D) refleja el contexto financiero activo.

---

## Categorías Seed

Insertadas automáticamente para cada empresa al ejecutar `scripts/finanzas_ddl.sql`:

| Nombre | Tipo |
|---|---|
| Servicios básicos | Gasto |
| Nómina | Gasto |
| Transporte | Gasto |
| Mantenimiento | Gasto |
| Compras menores | Gasto |
| Viáticos | Gasto |
| Ingreso extra | Ingreso |
| Préstamo recibido | Ingreso |
| Inversión | Ingreso |
| Otros | Ambos |

---

## Limitaciones Intencionales (V1)

- No hay conciliación bancaria
- No hay presupuestos ni comparación real vs. presupuestado
- No hay reportes financieros (balance, flujo de caja resumido)
- No hay integración con Ventas (las ventas no se registran aquí)
- No hay IVA ni cálculo fiscal
- No hay cuentas contables (plan de cuentas)
- El ContextoFinanciero=Usuario no tiene queries activas todavía

---

## Roadmap

1. **V1.1**: Filtros por categoría en grid, indicador visual de CxC/CxP vencidas (rojo)
2. **V1.2**: Resumen de flujo de caja (total gastos vs ingresos del período)
3. **V2**: Finanzas personales (contexto Usuario) activadas con queries separadas
4. **V3**: Presupuestos vs real, exportación a Excel
5. **V4**: Conciliación simple (ligar pagos a CxC/CxP)
6. **V-futuro**: Contexto Familia para grupos del hogar
