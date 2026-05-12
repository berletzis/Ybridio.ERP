IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505020147_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505020147_InitialSchema', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505020309_AddProductoTienda'
)
BEGIN
    CREATE TABLE [catalogos].[ProductoTienda] (
        [Id] int NOT NULL IDENTITY,
        [ProductoId] int NOT NULL,
        [TiendaId] int NOT NULL,
        [PrecioOverride] decimal(18,6) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        CONSTRAINT [PK_ProductoTienda] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductoTienda_Producto_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [catalogos].[Producto] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductoTienda_Tienda_TiendaId] FOREIGN KEY ([TiendaId]) REFERENCES [core].[Tienda] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505020309_AddProductoTienda'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductoTienda_ProductoId_TiendaId] ON [catalogos].[ProductoTienda] ([ProductoId], [TiendaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505020309_AddProductoTienda'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505020309_AddProductoTienda', N'8.0.26');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[Almacen] DROP CONSTRAINT [FK_Almacen_Tienda];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [finanzas].[Caja] DROP CONSTRAINT [FK_Caja_Tienda_TiendaId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Factura] DROP CONSTRAINT [FK_Factura_Cliente_ClienteId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] DROP CONSTRAINT [FK_Venta_Tienda];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DROP TABLE [catalogos].[ProductoTienda];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DROP TABLE [seguridad].[UsuarioTienda];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DROP TABLE [core].[Tienda];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DROP INDEX [IX_Almacen_EmpresaId] ON [inventario].[Almacen];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DROP INDEX [IX_Almacen_TiendaId] ON [inventario].[Almacen];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER SCHEMA [core] TRANSFER [catalogos].[Proveedor];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER SCHEMA [core] TRANSFER [catalogos].[ProductoCategoria];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER SCHEMA [core] TRANSFER [catalogos].[Producto];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER SCHEMA [core] TRANSFER [catalogos].[Cliente];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC sp_rename N'[ventas].[Venta].[TiendaId]', N'SucursalId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC sp_rename N'[ventas].[Venta].[IX_Venta_TiendaId]', N'IX_Venta_SucursalId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC sp_rename N'[ventas].[Venta].[IX_Venta_EmpresaId_TiendaId_Fecha]', N'IX_Venta_EmpresaId_SucursalId_Fecha', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC sp_rename N'[ventas].[Factura].[ClienteId]', N'RelacionComercialId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC sp_rename N'[ventas].[Factura].[IX_Factura_ClienteId]', N'IX_Factura_RelacionComercialId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC sp_rename N'[finanzas].[Caja].[TiendaId]', N'SucursalId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC sp_rename N'[finanzas].[Caja].[IX_Caja_TiendaId]', N'IX_Caja_SucursalId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC sp_rename N'[inventario].[Almacen].[TiendaId]', N'SucursalId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD [Estatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD [NombreCliente] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD [Observaciones] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD [PedidoId] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD [RelacionComercialId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD [Subtotal] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD [TipoPago] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD [TotalPagado] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[seguridad].[UsuarioToken]') AND [c].[name] = N'Name');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [seguridad].[UsuarioToken] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [seguridad].[UsuarioToken] ALTER COLUMN [Name] nvarchar(128) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[seguridad].[UsuarioToken]') AND [c].[name] = N'LoginProvider');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [seguridad].[UsuarioToken] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [seguridad].[UsuarioToken] ALTER COLUMN [LoginProvider] nvarchar(128) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[seguridad].[UsuarioLogin]') AND [c].[name] = N'ProviderKey');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [seguridad].[UsuarioLogin] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [seguridad].[UsuarioLogin] ALTER COLUMN [ProviderKey] nvarchar(128) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[seguridad].[UsuarioLogin]') AND [c].[name] = N'LoginProvider');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [seguridad].[UsuarioLogin] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [seguridad].[UsuarioLogin] ALTER COLUMN [LoginProvider] nvarchar(128) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[TipoMovimientoInventario] ADD [Descripcion] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[TipoMovimientoInventario] ADD [Signo] smallint NOT NULL DEFAULT CAST(1 AS smallint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[MovimientoInventario] ADD [Folio] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[MovimientoInventario] ADD [Observaciones] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[MovimientoInventario] ADD [SaldoAcumulado] decimal(18,6) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[MovimientoInventario] ADD [SucursalId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[Existencia] ADD [SucursalId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[Almacen] ADD [Activo] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[Almacen] ADD [Codigo] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[Almacen] ADD [Descripcion] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[Almacen] ADD [EsPrincipal] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [core].[Cliente] ADD [Direccion] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [core].[Cliente] ADD [LimiteCredito] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [core].[Cliente] ADD [Notas] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [core].[Cliente] ADD [Telefono] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [finanzas].[CategoriaFinanciera] (
        [Id] int NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [TipoAplicable] nvarchar(20) NOT NULL DEFAULT N'Ambos',
        [Nombre] nvarchar(100) NOT NULL,
        [Descripcion] nvarchar(300) NULL,
        [Color] nvarchar(20) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CategoriaFinanciera] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CategoriaFinanciera_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[ConceptoEntrada] (
        [Id] int NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [Descripcion] nvarchar(500) NULL,
        [AfectaExistencia] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RequiereOrdenCompra] bit NOT NULL DEFAULT CAST(0 AS bit),
        [EsTraspaso] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ConceptoEntrada] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConceptoEntrada_Empresa] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[ConceptoSalida] (
        [Id] int NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [Descripcion] nvarchar(500) NULL,
        [AfectaExistencia] bit NOT NULL DEFAULT CAST(1 AS bit),
        [EsTraspaso] bit NOT NULL DEFAULT CAST(0 AS bit),
        [EsVenta] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_ConceptoSalida] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConceptoSalida_Empresa] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [core].[EmpresaComercial] (
        [Id] int NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [RazonSocial] nvarchar(300) NOT NULL,
        [NombreComercial] nvarchar(300) NULL,
        [RFC] nvarchar(20) NULL,
        [Email] nvarchar(200) NULL,
        [Telefono] nvarchar(30) NULL,
        [Direccion] nvarchar(300) NULL,
        [Notas] nvarchar(500) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EmpresaComercial] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmpresaComercial_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[EstatusEntrada] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_EstatusEntrada] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[EstatusSalida] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_EstatusSalida] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [ventas].[PagoVenta] (
        [Id] bigint NOT NULL IDENTITY,
        [VentaId] bigint NOT NULL,
        [Fecha] datetime2 NOT NULL,
        [Monto] decimal(18,2) NOT NULL,
        [FormaPago] nvarchar(100) NOT NULL,
        [Referencia] nvarchar(200) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PagoVenta] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PagoVenta_Venta] FOREIGN KEY ([VentaId]) REFERENCES [ventas].[Venta] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [seguridad].[Perfil] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(100) NOT NULL,
        [Descripcion] nvarchar(500) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Perfil] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [core].[Sucursal] (
        [Id] int NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Sucursal] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sucursal_Empresa] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [seguridad].[UsuarioAlmacen] (
        [Id] int NOT NULL IDENTITY,
        [UsuarioId] uniqueidentifier NOT NULL,
        [AlmacenId] int NOT NULL,
        CONSTRAINT [PK_UsuarioAlmacen] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UsuarioAlmacen_Almacen] FOREIGN KEY ([AlmacenId]) REFERENCES [inventario].[Almacen] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UsuarioAlmacen_Usuario] FOREIGN KEY ([UsuarioId]) REFERENCES [seguridad].[Usuario] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [core].[Persona] (
        [Id] int NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [EmpresaComercialId] int NULL,
        [Nombre] nvarchar(200) NOT NULL,
        [Apellidos] nvarchar(200) NULL,
        [RFC] nvarchar(20) NULL,
        [Email] nvarchar(200) NULL,
        [Telefono] nvarchar(30) NULL,
        [Direccion] nvarchar(300) NULL,
        [Notas] nvarchar(500) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Persona] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Persona_EmpresaComercial_EmpresaComercialId] FOREIGN KEY ([EmpresaComercialId]) REFERENCES [core].[EmpresaComercial] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Persona_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [seguridad].[PerfilPermiso] (
        [Id] int NOT NULL IDENTITY,
        [PerfilId] int NOT NULL,
        [PermisoId] int NOT NULL,
        CONSTRAINT [PK_PerfilPermiso] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PerfilPermiso_Perfil] FOREIGN KEY ([PerfilId]) REFERENCES [seguridad].[Perfil] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PerfilPermiso_Permiso] FOREIGN KEY ([PermisoId]) REFERENCES [seguridad].[Permiso] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [seguridad].[UsuarioPerfil] (
        [Id] int NOT NULL IDENTITY,
        [UsuarioId] uniqueidentifier NOT NULL,
        [PerfilId] int NOT NULL,
        CONSTRAINT [PK_UsuarioPerfil] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UsuarioPerfil_Perfil] FOREIGN KEY ([PerfilId]) REFERENCES [seguridad].[Perfil] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UsuarioPerfil_Usuario] FOREIGN KEY ([UsuarioId]) REFERENCES [seguridad].[Usuario] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[AjusteInventario] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NOT NULL,
        [AlmacenId] int NOT NULL,
        [Folio] nvarchar(50) NULL,
        [Fecha] datetime2 NOT NULL DEFAULT (getdate()),
        [TipoAjuste] smallint NOT NULL DEFAULT CAST(1 AS smallint),
        [Motivo] nvarchar(500) NOT NULL,
        [UsuarioAutorizacionId] uniqueidentifier NULL,
        [Aplicado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [FechaAplicacion] datetime2 NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AjusteInventario] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AjusteInventario_Almacen] FOREIGN KEY ([AlmacenId]) REFERENCES [inventario].[Almacen] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AjusteInventario_Empresa] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AjusteInventario_Sucursal] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [finanzas].[CuentaPorCobrar] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NULL,
        [NombreDeudor] nvarchar(200) NOT NULL,
        [Concepto] nvarchar(200) NOT NULL,
        [MontoOriginal] decimal(18,2) NOT NULL,
        [MontoPagado] decimal(18,2) NOT NULL DEFAULT 0.0,
        [FechaEmision] datetime2 NOT NULL,
        [FechaVencimiento] datetime2 NOT NULL,
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CuentaPorCobrar] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CuentaPorCobrar_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CuentaPorCobrar_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [finanzas].[CuentaPorPagar] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NULL,
        [NombreAcreedor] nvarchar(200) NOT NULL,
        [Concepto] nvarchar(200) NOT NULL,
        [MontoOriginal] decimal(18,2) NOT NULL,
        [MontoPagado] decimal(18,2) NOT NULL DEFAULT 0.0,
        [FechaEmision] datetime2 NOT NULL,
        [FechaVencimiento] datetime2 NOT NULL,
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CuentaPorPagar] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CuentaPorPagar_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CuentaPorPagar_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [finanzas].[MovimientoFinanciero] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NULL,
        [Tipo] int NOT NULL,
        [Contexto] int NOT NULL DEFAULT 0,
        [UsuarioContextoId] uniqueidentifier NULL,
        [CategoriaId] int NULL,
        [Concepto] nvarchar(200) NOT NULL,
        [Monto] decimal(18,2) NOT NULL,
        [Fecha] datetime2 NOT NULL,
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MovimientoFinanciero] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MovimientoFinanciero_CategoriaFinanciera_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [finanzas].[CategoriaFinanciera] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_MovimientoFinanciero_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MovimientoFinanciero_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [core].[ProductoSucursal] (
        [Id] int NOT NULL IDENTITY,
        [ProductoId] int NOT NULL,
        [SucursalId] int NOT NULL,
        [PrecioOverride] decimal(18,6) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        CONSTRAINT [PK_ProductoSucursal] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductoSucursal_Producto_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [core].[Producto] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductoSucursal_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[Salida] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NOT NULL,
        [AlmacenId] int NOT NULL,
        [ConceptoSalidaId] int NOT NULL,
        [EstatusSalidaId] int NOT NULL,
        [Folio] nvarchar(50) NULL,
        [Fecha] datetime2 NOT NULL DEFAULT (getdate()),
        [VentaId] bigint NULL,
        [AlmacenDestinoId] int NULL,
        [UsuarioAutorizacionId] uniqueidentifier NULL,
        [Total] decimal(18,6) NOT NULL DEFAULT 0.0,
        [Observaciones] nvarchar(1000) NULL,
        [Aplicada] bit NOT NULL DEFAULT CAST(0 AS bit),
        [FechaAplicacion] datetime2 NULL,
        [UsuarioAplicacionId] uniqueidentifier NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Salida] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Salida_Almacen] FOREIGN KEY ([AlmacenId]) REFERENCES [inventario].[Almacen] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Salida_AlmacenDestino] FOREIGN KEY ([AlmacenDestinoId]) REFERENCES [inventario].[Almacen] ([Id]),
        CONSTRAINT [FK_Salida_ConceptoSalida] FOREIGN KEY ([ConceptoSalidaId]) REFERENCES [inventario].[ConceptoSalida] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Salida_Empresa] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Salida_EstatusSalida] FOREIGN KEY ([EstatusSalidaId]) REFERENCES [inventario].[EstatusSalida] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Salida_Sucursal] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Salida_Venta] FOREIGN KEY ([VentaId]) REFERENCES [ventas].[Venta] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [seguridad].[UsuarioSucursal] (
        [Id] int NOT NULL IDENTITY,
        [UsuarioId] uniqueidentifier NOT NULL,
        [SucursalId] int NULL,
        CONSTRAINT [PK_UsuarioSucursal] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UsuarioSucursal_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]),
        CONSTRAINT [FK_UsuarioSucursal_Usuario] FOREIGN KEY ([UsuarioId]) REFERENCES [seguridad].[Usuario] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [core].[RelacionComercial] (
        [Id] int NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [PersonaId] int NULL,
        [EmpresaComercialId] int NULL,
        [TipoRelacion] int NOT NULL,
        [LimiteCredito] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_RelacionComercial] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RelacionComercial_EmpresaComercial_EmpresaComercialId] FOREIGN KEY ([EmpresaComercialId]) REFERENCES [core].[EmpresaComercial] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RelacionComercial_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RelacionComercial_Persona_PersonaId] FOREIGN KEY ([PersonaId]) REFERENCES [core].[Persona] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[AjusteInventarioDetalle] (
        [Id] bigint NOT NULL IDENTITY,
        [AjusteInventarioId] bigint NOT NULL,
        [ProductoId] int NOT NULL,
        [CantidadSistema] decimal(18,6) NOT NULL DEFAULT 0.0,
        [CantidadFisica] decimal(18,6) NOT NULL DEFAULT 0.0,
        [Diferencia] AS [CantidadFisica] - [CantidadSistema] PERSISTED,
        [CostoUnitario] decimal(18,6) NOT NULL DEFAULT 0.0,
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AjusteInventarioDetalle] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AjusteDetalle_Ajuste] FOREIGN KEY ([AjusteInventarioId]) REFERENCES [inventario].[AjusteInventario] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AjusteDetalle_Producto] FOREIGN KEY ([ProductoId]) REFERENCES [core].[Producto] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[Entrada] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NOT NULL,
        [AlmacenId] int NOT NULL,
        [ConceptoEntradaId] int NOT NULL,
        [EstatusEntradaId] int NOT NULL,
        [Folio] nvarchar(50) NULL,
        [Fecha] datetime2 NOT NULL DEFAULT (getdate()),
        [FechaRecepcion] datetime2 NULL,
        [ReferenciaExterna] nvarchar(150) NULL,
        [NumeroFactura] nvarchar(100) NULL,
        [ProveedorId] int NULL,
        [OrdenCompraId] bigint NULL,
        [AlmacenOrigenId] int NULL,
        [SalidaOrigenId] bigint NULL,
        [Subtotal] decimal(18,6) NOT NULL DEFAULT 0.0,
        [TotalImpuestos] decimal(18,6) NOT NULL DEFAULT 0.0,
        [Total] decimal(18,6) NOT NULL DEFAULT 0.0,
        [Observaciones] nvarchar(1000) NULL,
        [Aplicada] bit NOT NULL DEFAULT CAST(0 AS bit),
        [FechaAplicacion] datetime2 NULL,
        [UsuarioAplicacionId] uniqueidentifier NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Entrada] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Entrada_Almacen] FOREIGN KEY ([AlmacenId]) REFERENCES [inventario].[Almacen] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Entrada_AlmacenOrigen] FOREIGN KEY ([AlmacenOrigenId]) REFERENCES [inventario].[Almacen] ([Id]),
        CONSTRAINT [FK_Entrada_ConceptoEntrada] FOREIGN KEY ([ConceptoEntradaId]) REFERENCES [inventario].[ConceptoEntrada] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Entrada_Empresa] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Entrada_EstatusEntrada] FOREIGN KEY ([EstatusEntradaId]) REFERENCES [inventario].[EstatusEntrada] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Entrada_OrdenCompra] FOREIGN KEY ([OrdenCompraId]) REFERENCES [compras].[OrdenCompra] ([Id]),
        CONSTRAINT [FK_Entrada_Proveedor] FOREIGN KEY ([ProveedorId]) REFERENCES [core].[Proveedor] ([Id]),
        CONSTRAINT [FK_Entrada_SalidaOrigen] FOREIGN KEY ([SalidaOrigenId]) REFERENCES [inventario].[Salida] ([Id]),
        CONSTRAINT [FK_Entrada_Sucursal] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [ventas].[Cotizacion] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NULL,
        [RelacionComercialId] int NULL,
        [NombreCliente] nvarchar(200) NOT NULL,
        [Estatus] int NOT NULL,
        [Fecha] datetime2 NOT NULL,
        [FechaVigencia] datetime2 NULL,
        [VendedorId] uniqueidentifier NULL,
        [Subtotal] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Total] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Cotizacion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Cotizacion_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Cotizacion_RelacionComercial_RelacionComercialId] FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Cotizacion_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[EntradaDetalle] (
        [Id] bigint NOT NULL IDENTITY,
        [EntradaId] bigint NOT NULL,
        [ProductoId] int NOT NULL,
        [NumeroLinea] smallint NOT NULL DEFAULT CAST(1 AS smallint),
        [CantidadEsperada] decimal(18,6) NOT NULL DEFAULT 0.0,
        [CantidadRecibida] decimal(18,6) NOT NULL DEFAULT 0.0,
        [CantidadCajas] int NULL,
        [PiezasPorCaja] int NULL,
        [CostoUnitario] decimal(18,6) NOT NULL DEFAULT 0.0,
        [Importe] decimal(18,6) NOT NULL DEFAULT 0.0,
        [CodigoBarras] nvarchar(100) NULL,
        [Sku] nvarchar(100) NULL,
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EntradaDetalle] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EntradaDetalle_Entrada] FOREIGN KEY ([EntradaId]) REFERENCES [inventario].[Entrada] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_EntradaDetalle_Producto] FOREIGN KEY ([ProductoId]) REFERENCES [core].[Producto] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[Traspaso] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [Folio] nvarchar(50) NULL,
        [Fecha] datetime2 NOT NULL DEFAULT (getdate()),
        [AlmacenOrigenId] int NOT NULL,
        [AlmacenDestinoId] int NOT NULL,
        [SalidaId] bigint NULL,
        [EntradaId] bigint NULL,
        [Estatus] int NOT NULL DEFAULT 1,
        [Observaciones] nvarchar(1000) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Traspaso] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Traspaso_AlmacenDestino] FOREIGN KEY ([AlmacenDestinoId]) REFERENCES [inventario].[Almacen] ([Id]),
        CONSTRAINT [FK_Traspaso_AlmacenOrigen] FOREIGN KEY ([AlmacenOrigenId]) REFERENCES [inventario].[Almacen] ([Id]),
        CONSTRAINT [FK_Traspaso_Empresa] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Traspaso_Entrada] FOREIGN KEY ([EntradaId]) REFERENCES [inventario].[Entrada] ([Id]),
        CONSTRAINT [FK_Traspaso_Salida] FOREIGN KEY ([SalidaId]) REFERENCES [inventario].[Salida] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [ventas].[CotizacionDetalle] (
        [Id] bigint NOT NULL IDENTITY,
        [CotizacionId] bigint NOT NULL,
        [ProductoId] int NULL,
        [Descripcion] nvarchar(300) NOT NULL,
        [Cantidad] decimal(18,6) NOT NULL,
        [PrecioUnitario] decimal(18,2) NOT NULL,
        [Importe] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_CotizacionDetalle] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CotizacionDetalle_Cotizacion_CotizacionId] FOREIGN KEY ([CotizacionId]) REFERENCES [ventas].[Cotizacion] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CotizacionDetalle_Producto_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [core].[Producto] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [ventas].[Pedido] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NULL,
        [RelacionComercialId] int NULL,
        [NombreCliente] nvarchar(200) NOT NULL,
        [CotizacionId] bigint NULL,
        [Estatus] int NOT NULL,
        [Fecha] datetime2 NOT NULL,
        [FechaEntregaCompromiso] datetime2 NULL,
        [Total] decimal(18,2) NOT NULL DEFAULT 0.0,
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Pedido] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Pedido_Cotizacion_CotizacionId] FOREIGN KEY ([CotizacionId]) REFERENCES [ventas].[Cotizacion] ([Id]),
        CONSTRAINT [FK_Pedido_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Pedido_RelacionComercial_RelacionComercialId] FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Pedido_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [inventario].[SalidaDetalle] (
        [Id] bigint NOT NULL IDENTITY,
        [SalidaId] bigint NOT NULL,
        [ProductoId] int NOT NULL,
        [NumeroLinea] smallint NOT NULL DEFAULT CAST(1 AS smallint),
        [Cantidad] decimal(18,6) NOT NULL,
        [CantidadCajas] int NULL,
        [PiezasPorCaja] int NULL,
        [CostoUnitario] decimal(18,6) NOT NULL DEFAULT 0.0,
        [Importe] decimal(18,6) NOT NULL DEFAULT 0.0,
        [PrecioUnitario] decimal(18,6) NULL,
        [Descuento] decimal(18,6) NULL,
        [EntradaDetalleOrigenId] bigint NULL,
        [CodigoBarras] nvarchar(100) NULL,
        [Sku] nvarchar(100) NULL,
        [Observaciones] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SalidaDetalle] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalidaDetalle_EntradaDetalleOrigen] FOREIGN KEY ([EntradaDetalleOrigenId]) REFERENCES [inventario].[EntradaDetalle] ([Id]),
        CONSTRAINT [FK_SalidaDetalle_Producto] FOREIGN KEY ([ProductoId]) REFERENCES [core].[Producto] ([Id]),
        CONSTRAINT [FK_SalidaDetalle_Salida] FOREIGN KEY ([SalidaId]) REFERENCES [inventario].[Salida] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [ventas].[OrdenTrabajo] (
        [Id] bigint NOT NULL IDENTITY,
        [EmpresaId] int NOT NULL,
        [SucursalId] int NULL,
        [RelacionComercialId] int NULL,
        [NombreCliente] nvarchar(200) NOT NULL,
        [PedidoId] bigint NULL,
        [Estatus] int NOT NULL,
        [Fecha] datetime2 NOT NULL,
        [FechaCompromiso] datetime2 NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [Observaciones] nvarchar(500) NULL,
        [ResponsableId] uniqueidentifier NULL,
        [Total] decimal(18,2) NOT NULL DEFAULT 0.0,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (getdate()),
        [UsuarioCreacionId] uniqueidentifier NOT NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        [Borrado] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_OrdenTrabajo] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrdenTrabajo_Empresa_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [core].[Empresa] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrdenTrabajo_Pedido_PedidoId] FOREIGN KEY ([PedidoId]) REFERENCES [ventas].[Pedido] ([Id]),
        CONSTRAINT [FK_OrdenTrabajo_RelacionComercial_RelacionComercialId] FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_OrdenTrabajo_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [ventas].[PedidoDetalle] (
        [Id] bigint NOT NULL IDENTITY,
        [PedidoId] bigint NOT NULL,
        [ProductoId] int NULL,
        [Descripcion] nvarchar(300) NOT NULL,
        [Cantidad] decimal(18,6) NOT NULL,
        [PrecioUnitario] decimal(18,2) NOT NULL,
        [Importe] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_PedidoDetalle] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PedidoDetalle_Pedido_PedidoId] FOREIGN KEY ([PedidoId]) REFERENCES [ventas].[Pedido] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PedidoDetalle_Producto_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [core].[Producto] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE TABLE [ventas].[OrdenTrabajoMaterial] (
        [Id] bigint NOT NULL IDENTITY,
        [OrdenTrabajoId] bigint NOT NULL,
        [ProductoId] int NULL,
        [Descripcion] nvarchar(300) NOT NULL,
        [Cantidad] decimal(18,6) NOT NULL,
        [PrecioUnitario] decimal(18,2) NOT NULL,
        [Importe] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrdenTrabajoMaterial] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrdenTrabajoMaterial_OrdenTrabajo_OrdenTrabajoId] FOREIGN KEY ([OrdenTrabajoId]) REFERENCES [ventas].[OrdenTrabajo] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrdenTrabajoMaterial_Producto_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [core].[Producto] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Venta_RelacionComercialId] ON [ventas].[Venta] ([RelacionComercialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_MovimientoInventario_SucursalId] ON [inventario].[MovimientoInventario] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Existencia_SucursalId] ON [inventario].[Existencia] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Almacen_EmpresaActivo] ON [inventario].[Almacen] ([EmpresaId], [SucursalId]) WHERE [Borrado] = 0 AND [Activo] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UQ_Almacen_Sucursal_Codigo] ON [inventario].[Almacen] ([SucursalId], [Codigo]) WHERE [Codigo] IS NOT NULL AND [Borrado] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_AjusteInventario_AlmacenId] ON [inventario].[AjusteInventario] ([AlmacenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_AjusteInventario_EmpresaId] ON [inventario].[AjusteInventario] ([EmpresaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_AjusteInventario_SucursalId] ON [inventario].[AjusteInventario] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_AjusteInventarioDetalle_AjusteInventarioId] ON [inventario].[AjusteInventarioDetalle] ([AjusteInventarioId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_AjusteInventarioDetalle_ProductoId] ON [inventario].[AjusteInventarioDetalle] ([ProductoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_CategoriaFinanciera_Empresa_Nombre] ON [finanzas].[CategoriaFinanciera] ([EmpresaId], [Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_ConceptoEntrada_EmpresaId] ON [inventario].[ConceptoEntrada] ([EmpresaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_ConceptoSalida_EmpresaId] ON [inventario].[ConceptoSalida] ([EmpresaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Cotizacion_Empresa_Estatus] ON [ventas].[Cotizacion] ([EmpresaId], [Estatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Cotizacion_Empresa_Fecha] ON [ventas].[Cotizacion] ([EmpresaId], [Fecha]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Cotizacion_RelacionComercialId] ON [ventas].[Cotizacion] ([RelacionComercialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Cotizacion_SucursalId] ON [ventas].[Cotizacion] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_CotizacionDetalle_CotizacionId] ON [ventas].[CotizacionDetalle] ([CotizacionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_CotizacionDetalle_ProductoId] ON [ventas].[CotizacionDetalle] ([ProductoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_CuentaPorCobrar_Empresa_Vencimiento] ON [finanzas].[CuentaPorCobrar] ([EmpresaId], [FechaVencimiento]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_CuentaPorCobrar_SucursalId] ON [finanzas].[CuentaPorCobrar] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_CuentaPorPagar_Empresa_Vencimiento] ON [finanzas].[CuentaPorPagar] ([EmpresaId], [FechaVencimiento]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_CuentaPorPagar_SucursalId] ON [finanzas].[CuentaPorPagar] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_EmpresaComercial_EmpresaId] ON [core].[EmpresaComercial] ([EmpresaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_EmpresaComercial_EmpresaId_RFC] ON [core].[EmpresaComercial] ([EmpresaId], [RFC]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_Almacen] ON [inventario].[Entrada] ([AlmacenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_AlmacenOrigenId] ON [inventario].[Entrada] ([AlmacenOrigenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_ConceptoEntradaId] ON [inventario].[Entrada] ([ConceptoEntradaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_EmpresaSucursal] ON [inventario].[Entrada] ([EmpresaId], [SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_EstatusEntradaId] ON [inventario].[Entrada] ([EstatusEntradaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Entrada_Folio] ON [inventario].[Entrada] ([Folio]) WHERE [Folio] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_OrdenCompraId] ON [inventario].[Entrada] ([OrdenCompraId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_ProveedorId] ON [inventario].[Entrada] ([ProveedorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_SalidaOrigenId] ON [inventario].[Entrada] ([SalidaOrigenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Entrada_SucursalId] ON [inventario].[Entrada] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_EntradaDetalle_Entrada] ON [inventario].[EntradaDetalle] ([EntradaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_EntradaDetalle_Producto] ON [inventario].[EntradaDetalle] ([ProductoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_MovimientoFinanciero_CategoriaId] ON [finanzas].[MovimientoFinanciero] ([CategoriaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_MovimientoFinanciero_Empresa_Sucursal_Fecha] ON [finanzas].[MovimientoFinanciero] ([EmpresaId], [SucursalId], [Fecha]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_MovimientoFinanciero_Empresa_Tipo_Fecha] ON [finanzas].[MovimientoFinanciero] ([EmpresaId], [Tipo], [Fecha]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_MovimientoFinanciero_SucursalId] ON [finanzas].[MovimientoFinanciero] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_OrdenTrabajo_Empresa_Estatus] ON [ventas].[OrdenTrabajo] ([EmpresaId], [Estatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_OrdenTrabajo_Empresa_Fecha] ON [ventas].[OrdenTrabajo] ([EmpresaId], [Fecha]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_OrdenTrabajo_PedidoId] ON [ventas].[OrdenTrabajo] ([PedidoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_OrdenTrabajo_RelacionComercialId] ON [ventas].[OrdenTrabajo] ([RelacionComercialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_OrdenTrabajo_SucursalId] ON [ventas].[OrdenTrabajo] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_OrdenTrabajoMaterial_OrdenTrabajoId] ON [ventas].[OrdenTrabajoMaterial] ([OrdenTrabajoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_OrdenTrabajoMaterial_ProductoId] ON [ventas].[OrdenTrabajoMaterial] ([ProductoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_PagoVenta_VentaId] ON [ventas].[PagoVenta] ([VentaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Pedido_CotizacionId] ON [ventas].[Pedido] ([CotizacionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Pedido_Empresa_Estatus] ON [ventas].[Pedido] ([EmpresaId], [Estatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Pedido_Empresa_Fecha] ON [ventas].[Pedido] ([EmpresaId], [Fecha]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Pedido_RelacionComercialId] ON [ventas].[Pedido] ([RelacionComercialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Pedido_SucursalId] ON [ventas].[Pedido] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_PedidoDetalle_PedidoId] ON [ventas].[PedidoDetalle] ([PedidoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_PedidoDetalle_ProductoId] ON [ventas].[PedidoDetalle] ([ProductoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Perfil_Nombre] ON [seguridad].[Perfil] ([Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_PerfilPermiso_PermisoId] ON [seguridad].[PerfilPermiso] ([PermisoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PerfilPermiso_PerfilPermiso] ON [seguridad].[PerfilPermiso] ([PerfilId], [PermisoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Persona_EmpresaComercialId] ON [core].[Persona] ([EmpresaComercialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Persona_EmpresaId] ON [core].[Persona] ([EmpresaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Persona_EmpresaId_RFC] ON [core].[Persona] ([EmpresaId], [RFC]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductoSucursal_ProductoId_SucursalId] ON [core].[ProductoSucursal] ([ProductoId], [SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_ProductoSucursal_SucursalId] ON [core].[ProductoSucursal] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_RelacionComercial_EmpresaComercialId] ON [core].[RelacionComercial] ([EmpresaComercialId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_RelacionComercial_EmpresaId_Tipo] ON [core].[RelacionComercial] ([EmpresaId], [TipoRelacion]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_RelacionComercial_PersonaId] ON [core].[RelacionComercial] ([PersonaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Salida_Almacen] ON [inventario].[Salida] ([AlmacenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Salida_AlmacenDestinoId] ON [inventario].[Salida] ([AlmacenDestinoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Salida_ConceptoSalidaId] ON [inventario].[Salida] ([ConceptoSalidaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Salida_EmpresaSucursal] ON [inventario].[Salida] ([EmpresaId], [SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Salida_EstatusSalidaId] ON [inventario].[Salida] ([EstatusSalidaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Salida_Folio] ON [inventario].[Salida] ([Folio]) WHERE [Folio] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Salida_SucursalId] ON [inventario].[Salida] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Salida_VentaId] ON [inventario].[Salida] ([VentaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_SalidaDetalle_EntradaDetalleOrigenId] ON [inventario].[SalidaDetalle] ([EntradaDetalleOrigenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_SalidaDetalle_Producto] ON [inventario].[SalidaDetalle] ([ProductoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_SalidaDetalle_Salida] ON [inventario].[SalidaDetalle] ([SalidaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Sucursal_EmpresaId] ON [core].[Sucursal] ([EmpresaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Traspaso_AlmacenDestinoId] ON [inventario].[Traspaso] ([AlmacenDestinoId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Traspaso_AlmacenOrigenId] ON [inventario].[Traspaso] ([AlmacenOrigenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Traspaso_Empresa] ON [inventario].[Traspaso] ([EmpresaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Traspaso_EntradaId] ON [inventario].[Traspaso] ([EntradaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_Traspaso_SalidaId] ON [inventario].[Traspaso] ([SalidaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_UsuarioAlmacen_AlmacenId] ON [seguridad].[UsuarioAlmacen] ([AlmacenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_UsuarioAlmacen_UsuarioId] ON [seguridad].[UsuarioAlmacen] ([UsuarioId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE UNIQUE INDEX [UX_UsuarioAlmacen_UsuarioAlmacen] ON [seguridad].[UsuarioAlmacen] ([UsuarioId], [AlmacenId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_UsuarioPerfil_PerfilId] ON [seguridad].[UsuarioPerfil] ([PerfilId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE UNIQUE INDEX [UX_UsuarioPerfil_UsuarioPerfil] ON [seguridad].[UsuarioPerfil] ([UsuarioId], [PerfilId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_UsuarioSucursal_SucursalId] ON [seguridad].[UsuarioSucursal] ([SucursalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    CREATE INDEX [IX_UsuarioSucursal_UsuarioId] ON [seguridad].[UsuarioSucursal] ([UsuarioId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[Almacen] ADD CONSTRAINT [FK_Almacen_Sucursal] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [finanzas].[Caja] ADD CONSTRAINT [FK_Caja_Sucursal_SucursalId] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[Existencia] ADD CONSTRAINT [FK_Existencia_Sucursal] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Factura] ADD CONSTRAINT [FK_Factura_RelacionComercial_RelacionComercialId] FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [inventario].[MovimientoInventario] ADD CONSTRAINT [FK_MovimientoInventario_Sucursal] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD CONSTRAINT [FK_Venta_Cliente] FOREIGN KEY ([RelacionComercialId]) REFERENCES [core].[RelacionComercial] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    ALTER TABLE [ventas].[Venta] ADD CONSTRAINT [FK_Venta_Sucursal] FOREIGN KEY ([SucursalId]) REFERENCES [core].[Sucursal] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260511210551_AddBusinessPartnerModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260511210551_AddBusinessPartnerModel', N'8.0.26');
END;
GO

COMMIT;
GO

