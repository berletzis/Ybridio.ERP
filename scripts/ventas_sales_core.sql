-- ============================================================
-- Sales Core Operacional PYME — DDL + Seed (2026-05-08)
-- Cotizaciones, Pedidos, OrdenesTrabajo + extension Cliente
-- ============================================================

USE [YBRIDIO-26];

-- ── 1. Extender tabla core.Cliente ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('core.Cliente') AND name='Telefono')
BEGIN
    ALTER TABLE [core].[Cliente]
        ADD [Telefono]      NVARCHAR(30)   NULL,
            [Direccion]     NVARCHAR(300)  NULL,
            [Notas]         NVARCHAR(500)  NULL,
            [LimiteCredito] DECIMAL(18,2)  NOT NULL DEFAULT 0;
    PRINT 'core.Cliente ampliado con Telefono, Direccion, Notas, LimiteCredito.';
END
ELSE PRINT 'core.Cliente ya tiene columnas Sales Core — omitiendo.';

-- ── 2. ventas.Cotizacion ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Cotizacion' AND schema_id=SCHEMA_ID('ventas'))
BEGIN
    CREATE TABLE [ventas].[Cotizacion] (
        [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
        [EmpresaId]        INT              NOT NULL,
        [SucursalId]       INT              NULL,
        [ClienteId]        INT              NULL,
        [NombreCliente]    NVARCHAR(200)    NOT NULL DEFAULT '',
        [Estatus]          INT              NOT NULL DEFAULT 0,
        [Fecha]            DATETIME         NOT NULL,
        [FechaVigencia]    DATETIME         NULL,
        [VendedorId]       UNIQUEIDENTIFIER NULL,
        [Subtotal]         DECIMAL(18,2)    NOT NULL DEFAULT 0,
        [Total]            DECIMAL(18,2)    NOT NULL DEFAULT 0,
        [Observaciones]    NVARCHAR(500)    NULL,
        [FechaCreacion]    DATETIME         NOT NULL DEFAULT GETDATE(),
        [UsuarioCreacionId] UNIQUEIDENTIFIER NOT NULL,
        [FechaModificacion] DATETIME        NULL,
        [UsuarioModificacionId] UNIQUEIDENTIFIER NULL,
        [Borrado]          BIT              NOT NULL DEFAULT 0,
        [RowVersion]       ROWVERSION       NOT NULL,
        CONSTRAINT [PK_Cotizacion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Cotizacion_Empresa]   FOREIGN KEY ([EmpresaId])  REFERENCES [core].[Empresa]([Id]),
        CONSTRAINT [FK_Cotizacion_Sucursal]  FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal]([Id]),
        CONSTRAINT [FK_Cotizacion_Cliente]   FOREIGN KEY ([ClienteId])  REFERENCES [core].[Cliente]([Id]) ON DELETE SET NULL
    );
    CREATE INDEX [IX_Cotizacion_Empresa_Fecha]   ON [ventas].[Cotizacion]([EmpresaId],[Fecha]);
    CREATE INDEX [IX_Cotizacion_Empresa_Estatus] ON [ventas].[Cotizacion]([EmpresaId],[Estatus]);
    PRINT 'ventas.Cotizacion CREADA.';
END
ELSE PRINT 'ventas.Cotizacion ya existe.';

-- ── 3. ventas.CotizacionDetalle ───────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='CotizacionDetalle' AND schema_id=SCHEMA_ID('ventas'))
BEGIN
    CREATE TABLE [ventas].[CotizacionDetalle] (
        [Id]              BIGINT          IDENTITY(1,1) NOT NULL,
        [CotizacionId]    BIGINT          NOT NULL,
        [ProductoId]      INT             NULL,
        [Descripcion]     NVARCHAR(300)   NOT NULL,
        [Cantidad]        DECIMAL(18,6)   NOT NULL,
        [PrecioUnitario]  DECIMAL(18,2)   NOT NULL,
        [Importe]         DECIMAL(18,2)   NOT NULL,
        CONSTRAINT [PK_CotizacionDetalle] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CotDetalle_Cotizacion] FOREIGN KEY ([CotizacionId]) REFERENCES [ventas].[Cotizacion]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CotDetalle_Producto]   FOREIGN KEY ([ProductoId])   REFERENCES [core].[Producto]([Id])    ON DELETE SET NULL
    );
    PRINT 'ventas.CotizacionDetalle CREADA.';
END
ELSE PRINT 'ventas.CotizacionDetalle ya existe.';

-- ── 4. ventas.Pedido ─────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Pedido' AND schema_id=SCHEMA_ID('ventas'))
BEGIN
    CREATE TABLE [ventas].[Pedido] (
        [Id]                     BIGINT           IDENTITY(1,1) NOT NULL,
        [EmpresaId]              INT              NOT NULL,
        [SucursalId]             INT              NULL,
        [ClienteId]              INT              NULL,
        [NombreCliente]          NVARCHAR(200)    NOT NULL DEFAULT '',
        [CotizacionId]           BIGINT           NULL,
        [Estatus]                INT              NOT NULL DEFAULT 0,
        [Fecha]                  DATETIME         NOT NULL,
        [FechaEntregaCompromiso] DATETIME         NULL,
        [Total]                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        [Observaciones]          NVARCHAR(500)    NULL,
        [FechaCreacion]          DATETIME         NOT NULL DEFAULT GETDATE(),
        [UsuarioCreacionId]      UNIQUEIDENTIFIER NOT NULL,
        [FechaModificacion]      DATETIME         NULL,
        [UsuarioModificacionId]  UNIQUEIDENTIFIER NULL,
        [Borrado]                BIT              NOT NULL DEFAULT 0,
        [RowVersion]             ROWVERSION       NOT NULL,
        CONSTRAINT [PK_Pedido] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Pedido_Empresa]     FOREIGN KEY ([EmpresaId])    REFERENCES [core].[Empresa]([Id]),
        CONSTRAINT [FK_Pedido_Sucursal]    FOREIGN KEY ([SucursalId])   REFERENCES [core].[Sucursal]([Id]),
        CONSTRAINT [FK_Pedido_Cliente]     FOREIGN KEY ([ClienteId])    REFERENCES [core].[Cliente]([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Pedido_Cotizacion]  FOREIGN KEY ([CotizacionId]) REFERENCES [ventas].[Cotizacion]([Id])
    );
    CREATE INDEX [IX_Pedido_Empresa_Fecha]   ON [ventas].[Pedido]([EmpresaId],[Fecha]);
    CREATE INDEX [IX_Pedido_Empresa_Estatus] ON [ventas].[Pedido]([EmpresaId],[Estatus]);
    PRINT 'ventas.Pedido CREADA.';
END
ELSE PRINT 'ventas.Pedido ya existe.';

-- ── 5. ventas.PedidoDetalle ───────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='PedidoDetalle' AND schema_id=SCHEMA_ID('ventas'))
BEGIN
    CREATE TABLE [ventas].[PedidoDetalle] (
        [Id]              BIGINT          IDENTITY(1,1) NOT NULL,
        [PedidoId]        BIGINT          NOT NULL,
        [ProductoId]      INT             NULL,
        [Descripcion]     NVARCHAR(300)   NOT NULL,
        [Cantidad]        DECIMAL(18,6)   NOT NULL,
        [PrecioUnitario]  DECIMAL(18,2)   NOT NULL,
        [Importe]         DECIMAL(18,2)   NOT NULL,
        CONSTRAINT [PK_PedidoDetalle] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PedDetalle_Pedido]   FOREIGN KEY ([PedidoId])   REFERENCES [ventas].[Pedido]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PedDetalle_Producto] FOREIGN KEY ([ProductoId]) REFERENCES [core].[Producto]([Id]) ON DELETE SET NULL
    );
    PRINT 'ventas.PedidoDetalle CREADA.';
END
ELSE PRINT 'ventas.PedidoDetalle ya existe.';

-- ── 6. ventas.OrdenTrabajo ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='OrdenTrabajo' AND schema_id=SCHEMA_ID('ventas'))
BEGIN
    CREATE TABLE [ventas].[OrdenTrabajo] (
        [Id]               BIGINT           IDENTITY(1,1) NOT NULL,
        [EmpresaId]        INT              NOT NULL,
        [SucursalId]       INT              NULL,
        [ClienteId]        INT              NULL,
        [NombreCliente]    NVARCHAR(200)    NOT NULL DEFAULT '',
        [PedidoId]         BIGINT           NULL,
        [Estatus]          INT              NOT NULL DEFAULT 0,
        [Fecha]            DATETIME         NOT NULL,
        [FechaCompromiso]  DATETIME         NULL,
        [Descripcion]      NVARCHAR(500)    NOT NULL,
        [Observaciones]    NVARCHAR(500)    NULL,
        [ResponsableId]    UNIQUEIDENTIFIER NULL,
        [Total]            DECIMAL(18,2)    NOT NULL DEFAULT 0,
        [FechaCreacion]    DATETIME         NOT NULL DEFAULT GETDATE(),
        [UsuarioCreacionId] UNIQUEIDENTIFIER NOT NULL,
        [FechaModificacion] DATETIME        NULL,
        [UsuarioModificacionId] UNIQUEIDENTIFIER NULL,
        [Borrado]          BIT              NOT NULL DEFAULT 0,
        [RowVersion]       ROWVERSION       NOT NULL,
        CONSTRAINT [PK_OrdenTrabajo] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OT_Empresa]   FOREIGN KEY ([EmpresaId])  REFERENCES [core].[Empresa]([Id]),
        CONSTRAINT [FK_OT_Sucursal]  FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal]([Id]),
        CONSTRAINT [FK_OT_Cliente]   FOREIGN KEY ([ClienteId])  REFERENCES [core].[Cliente]([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_OT_Pedido]    FOREIGN KEY ([PedidoId])   REFERENCES [ventas].[Pedido]([Id])
    );
    CREATE INDEX [IX_OrdenTrabajo_Empresa_Estatus] ON [ventas].[OrdenTrabajo]([EmpresaId],[Estatus]);
    CREATE INDEX [IX_OrdenTrabajo_Empresa_Fecha]   ON [ventas].[OrdenTrabajo]([EmpresaId],[Fecha]);
    PRINT 'ventas.OrdenTrabajo CREADA.';
END
ELSE PRINT 'ventas.OrdenTrabajo ya existe.';

-- ── 7. ventas.OrdenTrabajoMaterial ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='OrdenTrabajoMaterial' AND schema_id=SCHEMA_ID('ventas'))
BEGIN
    CREATE TABLE [ventas].[OrdenTrabajoMaterial] (
        [Id]               BIGINT          IDENTITY(1,1) NOT NULL,
        [OrdenTrabajoId]   BIGINT          NOT NULL,
        [ProductoId]       INT             NULL,
        [Descripcion]      NVARCHAR(300)   NOT NULL,
        [Cantidad]         DECIMAL(18,6)   NOT NULL,
        [PrecioUnitario]   DECIMAL(18,2)   NOT NULL,
        [Importe]          DECIMAL(18,2)   NOT NULL,
        CONSTRAINT [PK_OrdenTrabajoMaterial] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OTMat_OT]       FOREIGN KEY ([OrdenTrabajoId]) REFERENCES [ventas].[OrdenTrabajo]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OTMat_Producto] FOREIGN KEY ([ProductoId])     REFERENCES [core].[Producto]([Id])      ON DELETE SET NULL
    );
    PRINT 'ventas.OrdenTrabajoMaterial CREADA.';
END
ELSE PRINT 'ventas.OrdenTrabajoMaterial ya existe.';

-- ── 8. Seguridad: Módulos y Permisos Sales Core ───────────────
DECLARE @maxOrden INT = (SELECT ISNULL(MAX(Orden),0) FROM [seguridad].[Modulo]);

IF NOT EXISTS (SELECT 1 FROM [seguridad].[Modulo] WHERE Clave='cotizacion')
    INSERT INTO [seguridad].[Modulo](Nombre,Clave,Orden,FechaCreacion,Borrado)
    VALUES('Cotizaciones','cotizacion',@maxOrden+1,GETDATE(),0);
SET @maxOrden = @maxOrden + 1;

IF NOT EXISTS (SELECT 1 FROM [seguridad].[Modulo] WHERE Clave='pedido')
    INSERT INTO [seguridad].[Modulo](Nombre,Clave,Orden,FechaCreacion,Borrado)
    VALUES('Pedidos','pedido',@maxOrden+1,GETDATE(),0);
SET @maxOrden = @maxOrden + 1;

IF NOT EXISTS (SELECT 1 FROM [seguridad].[Modulo] WHERE Clave='ordentrabajo')
    INSERT INTO [seguridad].[Modulo](Nombre,Clave,Orden,FechaCreacion,Borrado)
    VALUES('Órdenes de Trabajo','ordentrabajo',@maxOrden+1,GETDATE(),0);

-- Permisos Cotizacion
DECLARE @modCotId INT = (SELECT Id FROM [seguridad].[Modulo] WHERE Clave='cotizacion');
INSERT INTO [seguridad].[Permiso](ModuloId,Nombre,Clave,FechaCreacion,Borrado)
SELECT @modCotId,p.Nombre,p.Clave,GETDATE(),0 FROM (VALUES
    ('Ver cotizaciones',     'cotizacion.ver'),
    ('Crear cotizaciones',   'cotizacion.crear'),
    ('Editar cotizaciones',  'cotizacion.editar'),
    ('Cancelar cotizaciones','cotizacion.cancelar')
) p(Nombre,Clave) WHERE NOT EXISTS(SELECT 1 FROM [seguridad].[Permiso] WHERE Clave=p.Clave);

-- Permisos Pedido
DECLARE @modPedId INT = (SELECT Id FROM [seguridad].[Modulo] WHERE Clave='pedido');
INSERT INTO [seguridad].[Permiso](ModuloId,Nombre,Clave,FechaCreacion,Borrado)
SELECT @modPedId,p.Nombre,p.Clave,GETDATE(),0 FROM (VALUES
    ('Ver pedidos',     'pedido.ver'),
    ('Crear pedidos',   'pedido.crear'),
    ('Editar pedidos',  'pedido.editar'),
    ('Cancelar pedidos','pedido.cancelar')
) p(Nombre,Clave) WHERE NOT EXISTS(SELECT 1 FROM [seguridad].[Permiso] WHERE Clave=p.Clave);

-- Permisos OrdenTrabajo
DECLARE @modOTId INT = (SELECT Id FROM [seguridad].[Modulo] WHERE Clave='ordentrabajo');
INSERT INTO [seguridad].[Permiso](ModuloId,Nombre,Clave,FechaCreacion,Borrado)
SELECT @modOTId,p.Nombre,p.Clave,GETDATE(),0 FROM (VALUES
    ('Ver órdenes de trabajo',        'ordentrabajo.ver'),
    ('Crear órdenes de trabajo',      'ordentrabajo.crear'),
    ('Actualizar órdenes de trabajo', 'ordentrabajo.actualizar'),
    ('Cerrar órdenes de trabajo',     'ordentrabajo.cerrar')
) p(Nombre,Clave) WHERE NOT EXISTS(SELECT 1 FROM [seguridad].[Permiso] WHERE Clave=p.Clave);

PRINT 'Permisos Sales Core insertados.';

-- ── 9. Asignar a roles ────────────────────────────────────────
DECLARE @superAdminId UNIQUEIDENTIFIER = (SELECT Id FROM [seguridad].[Rol] WHERE NormalizedName='SUPERADMIN');
DECLARE @adminId      UNIQUEIDENTIFIER = (SELECT Id FROM [seguridad].[Rol] WHERE NormalizedName='ADMINISTRADOREMPRESA');
DECLARE @gerenteId    UNIQUEIDENTIFIER = (SELECT Id FROM [seguridad].[Rol] WHERE NormalizedName='GERENTESUCURSAL');
DECLARE @vendedorId   UNIQUEIDENTIFIER = (SELECT Id FROM [seguridad].[Rol] WHERE NormalizedName='VENDEDOR');
DECLARE @supervisorVId UNIQUEIDENTIFIER = (SELECT Id FROM [seguridad].[Rol] WHERE NormalizedName='SUPERVISORVENTAS');

-- SuperAdmin y Admin: todos
INSERT INTO [seguridad].[RolPermiso](RolId,PermisoId,Permitido)
SELECT r.Id,p.Id,1 FROM [seguridad].[Permiso] p
CROSS JOIN (VALUES(@superAdminId),(@adminId)) r(Id)
WHERE p.Clave IN('cotizacion.ver','cotizacion.crear','cotizacion.editar','cotizacion.cancelar',
                 'pedido.ver','pedido.crear','pedido.editar','pedido.cancelar',
                 'ordentrabajo.ver','ordentrabajo.crear','ordentrabajo.actualizar','ordentrabajo.cerrar')
  AND r.Id IS NOT NULL
  AND NOT EXISTS(SELECT 1 FROM [seguridad].[RolPermiso] WHERE RolId=r.Id AND PermisoId=p.Id);

-- Gerente Sucursal: todo menos cancelar
INSERT INTO [seguridad].[RolPermiso](RolId,PermisoId,Permitido)
SELECT @gerenteId,p.Id,1 FROM [seguridad].[Permiso] p
WHERE p.Clave IN('cotizacion.ver','cotizacion.crear','cotizacion.editar',
                 'pedido.ver','pedido.crear','pedido.editar',
                 'ordentrabajo.ver','ordentrabajo.crear','ordentrabajo.actualizar','ordentrabajo.cerrar')
  AND NOT EXISTS(SELECT 1 FROM [seguridad].[RolPermiso] WHERE RolId=@gerenteId AND PermisoId=p.Id);

-- Vendedor: ver + crear cotizaciones y pedidos; OT solo ver
INSERT INTO [seguridad].[RolPermiso](RolId,PermisoId,Permitido)
SELECT @vendedorId,p.Id,1 FROM [seguridad].[Permiso] p
WHERE p.Clave IN('cotizacion.ver','cotizacion.crear','pedido.ver','pedido.crear','ordentrabajo.ver')
  AND NOT EXISTS(SELECT 1 FROM [seguridad].[RolPermiso] WHERE RolId=@vendedorId AND PermisoId=p.Id);

-- SupervisorVentas: todo
INSERT INTO [seguridad].[RolPermiso](RolId,PermisoId,Permitido)
SELECT @supervisorVId,p.Id,1 FROM [seguridad].[Permiso] p
WHERE p.Clave IN('cotizacion.ver','cotizacion.crear','cotizacion.editar','cotizacion.cancelar',
                 'pedido.ver','pedido.crear','pedido.editar','pedido.cancelar',
                 'ordentrabajo.ver','ordentrabajo.crear','ordentrabajo.actualizar','ordentrabajo.cerrar')
  AND NOT EXISTS(SELECT 1 FROM [seguridad].[RolPermiso] WHERE RolId=@supervisorVId AND PermisoId=p.Id);

PRINT 'RolPermiso Sales Core asignados.';
PRINT 'SALES CORE — SQL COMPLETADO.';
