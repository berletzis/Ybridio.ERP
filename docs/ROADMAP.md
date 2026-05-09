# ROADMAP — Ybridio ERP

> Última actualización: 2026-05-08  
> Estado: plataforma base enterprise sólida. Entrando a fase de funcionalidad operacional completa.

---

## Estado Actual (Baseline)

La plataforma cuenta con:
- Arquitectura Clean Architecture en 4 capas (Domain → Infrastructure → Application → WinUI)
- RBAC + Profiles + Security Scopes (Security Foundation completo)
- Runtime Security Enforcement en módulos críticos
- Runtime Observability (Diagnostic Panel)
- WorkspaceService (tabs persistentes)
- Inventario operativo (Entradas, Salidas, Existencias, Productos)
- POS (Ventas, Caja)
- Seguridad administrativa (Usuarios, Roles, Perfiles, Scopes)
- Finanzas Operativas V1 (Gastos, Ingresos, CxC, CxP)
- Multiempresa / Multisucursal / Multialmacén

---

## Fase 1 — Consolidación Operacional (Próximos)

> Objetivo: completar los módulos existentes con funcionalidad real antes de agregar más.

### 1.1 Inventario Completo

| Item | Prioridad | Estado |
|---|---|---|
| Servicio de creación de Entradas (con UI) | Alta | Pendiente |
| Servicio de creación de Salidas (con UI) | Alta | Pendiente |
| Autorización visual en botón Salida.Autorizar | Alta | Pendiente |
| Kardex con datos reales | Media | Pendiente |
| Conteo físico operativo | Media | Pendiente |
| Traspasos entre almacenes | Media | Pendiente |
| Ajustes de inventario | Baja | Pendiente |

### 1.2 Compras

| Item | Prioridad | Estado |
|---|---|---|
| Órdenes de Compra con UI completa | Alta | Pendiente |
| Recepción de compra → actualiza inventario | Alta | Pendiente |
| Enlace OC ↔ Entrada de inventario | Media | Pendiente |

### 1.3 Finanzas V1.1

| Item | Prioridad | Estado |
|---|---|---|
| Indicador visual CxC/CxP vencidas (color rojo) | Alta | Pendiente |
| Resumen flujo de caja por período | Alta | Pendiente |
| Filtro por categoría en grids Gastos/Ingresos | Media | Pendiente |
| Totales en statusbar (suma del período) | Media | Pendiente |

### 1.4 Security Enforcement Completo

| Item | Prioridad | Estado |
|---|---|---|
| Enforcement en módulo POS | Alta | Pendiente |
| Enforcement en Compras | Alta | Pendiente |
| Enforcement en Contactos (Clientes/Proveedores) | Media | Pendiente |
| Guard `producto.ver` en service layer | Media | Pendiente |
| UI de UsuarioPermiso (overrides individuales) | Media | Pendiente |
| Ocultamiento visual de botones por permiso | Baja | Pendiente |

---

## Fase 2 — Experiencia Operacional Completa

> Objetivo: que un negocio real pueda operar el ERP día a día sin fricciones.

### 2.1 Gestión de Contactos

| Item | Descripción |
|---|---|
| Clientes completo | Alta/Baja, historial de compras, saldo pendiente CxC |
| Proveedores completo | Enlace a Compras y CxP |
| Buscador global | Buscar cliente/proveedor desde POS y Compras |

### 2.2 Reportes Operacionales

| Reporte | Módulo origen |
|---|---|
| Ventas por período / vendedor / sucursal | Ventas |
| Inventario actual por almacén | Inventario |
| Movimientos de caja por apertura | Finanzas |
| Gastos por categoría y período | Finanzas |
| CxC vencidas vs vigentes | Finanzas |
| CxP próximas a vencer | Finanzas |
| Kardex de producto | Inventario |

### 2.3 Facturación

| Item | Descripción |
|---|---|
| PDF de factura | Generación desde Venta |
| Folio automático por empresa | Configuración Global |
| Impresión de ticket POS | Configuración Tienda |

### 2.4 Notificaciones y Alertas

| Alerta | Trigger |
|---|---|
| Stock mínimo alcanzado | Descontar inventario |
| CxC vencida | Diario |
| CxP próxima a vencer | 3 días antes |
| Caja abierta sin cerrar | Al día siguiente |

---

## Fase 3 — Plataforma Multi-usuario / Multi-dispositivo

### 3.1 Finanzas Personales

- Contexto `ContextoFinanciero.Usuario` activado con queries separadas
- UI separada del módulo empresa (o toggle en Finanzas)
- Sin mezcla de datos empresa ↔ personales

### 3.2 Acceso Remoto / API

| Item | Descripción |
|---|---|
| ASP.NET Core Web API | Exposición de endpoints para apps móviles |
| JWT Auth | Reutilizar Security Foundation existente |
| App móvil POS | Consultar existencias, registrar venta simple |

### 3.3 Integración Contable (Opcional)

> Solo si el usuario lo requiere — NO es parte del roadmap por defecto.

- Exportación de movimientos a formato contable
- Conexión con software contable externo via API
- Generación de pólizas básicas (sin contabilidad interna)

---

## Fase 4 — Infraestructura Enterprise

### 4.1 Performance

| Item | Descripción |
|---|---|
| Redis para `IPermissionCache` | La interfaz ya existe, solo cambiar implementación |
| Índices de BD adicionales | Basados en queries reales del sistema |
| Paginación en grids grandes | Reemplazar carga total en módulos con >10k registros |

### 4.2 Resiliencia

| Item | Descripción |
|---|---|
| Retry policies | EF Core + Polly para conexión SQL |
| Circuit breaker | Para operaciones críticas de inventario |
| Background jobs | Alertas automáticas, reportes programados |

### 4.3 Testing

| Item | Descripción |
|---|---|
| Unit tests Application layer | Servicios críticos: Inventario, Ventas, Autorización |
| Integration tests Infrastructure | EF Core queries con BD de prueba |
| UI tests (manual checklist) | Flujos críticos: POS, Cierre de Caja, Autorización Salida |

---

## Decisiones de Roadmap Confirmadas

- **Sin contabilidad fiscal interna**: el ERP no incluirá diario general, balanzas ni conexión SAT.
- **Sin producción/manufactura**: fuera del scope de PYME comercial.
- **Sin multi-moneda V1**: una empresa = una moneda.
- **Sin e-commerce integrado**: las ventas se registran manualmente o via API en Fase 3.

---

## Deuda Técnica Conocida

| Item | Impacto | Dificultad |
|---|---|---|
| `ListarPorEmpresaAsync` en ProductoService sin guard en service | Bajo (ViewModel protege) | Requiere cambio de firma |
| `ObservableCollection<object>` → tipado (resuelto en Entradas/Salidas) | Ninguno | Ya resuelto |
| MVVMTK0045 warnings (128) | Ninguno en runtime | Requiere migrar a partial properties |
| Cadena de conexión hardcodeada | Solo en dev | Mover a config en producción |
