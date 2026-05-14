SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- ══════════════════════════════════════════════════════════════════════════════
-- AddConfigTables_V1.sql
-- Agrega las tablas de Configuración Global al esquema catalogos.
-- Idempotente — se puede ejecutar múltiples veces sin error.
--
-- Tablas creadas:
--   catalogos.ParametroGlobal  — parámetros de configuración operacional
--   catalogos.OtroCargo        — cargos accesorios documentales
--
-- BD destino: YBRIDIO-26
-- Fecha: 2026-05-13
-- ══════════════════════════════════════════════════════════════════════════════

-- ── catalogos.ParametroGlobal ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'catalogos' AND TABLE_NAME = 'ParametroGlobal'
)
BEGIN
    CREATE TABLE catalogos.ParametroGlobal (
        Id                    INT              NOT NULL IDENTITY(1,1),
        EmpresaId             INT              NOT NULL,
        Clave                 NVARCHAR(150)    NOT NULL,
        Valor                 NVARCHAR(500)    NOT NULL,
        Descripcion           NVARCHAR(500)    NULL,
        TipoDato              NVARCHAR(20)     NOT NULL DEFAULT 'string',
        Grupo                 NVARCHAR(100)    NOT NULL DEFAULT 'General',
        OrdenVisual           INT              NOT NULL DEFAULT 0,
        Activo                BIT              NOT NULL DEFAULT 1,

        -- Auditoría (AuditableEntity)
        FechaCreacion         DATETIME         NOT NULL DEFAULT GETDATE(),
        FechaModificacion     DATETIME         NULL,
        UsuarioCreacionId     UNIQUEIDENTIFIER NOT NULL,
        UsuarioModificacionId UNIQUEIDENTIFIER NULL,
        Borrado               BIT              NOT NULL DEFAULT 0,
        RowVersion            ROWVERSION       NOT NULL,

        CONSTRAINT PK_ParametroGlobal PRIMARY KEY (Id),
        CONSTRAINT FK_ParametroGlobal_Empresa FOREIGN KEY (EmpresaId)
            REFERENCES core.Empresa (Id)
    );

    CREATE UNIQUE INDEX UQ_ParametroGlobal_EmpresaClave
        ON catalogos.ParametroGlobal (EmpresaId, Clave)
        WHERE Borrado = 0;

    CREATE INDEX IX_ParametroGlobal_EmpresaId
        ON catalogos.ParametroGlobal (EmpresaId);

    PRINT 'Tabla catalogos.ParametroGlobal creada.';
END
ELSE
    PRINT 'Tabla catalogos.ParametroGlobal ya existe — omitida.';

-- ── catalogos.OtroCargo ────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'catalogos' AND TABLE_NAME = 'OtroCargo'
)
BEGIN
    CREATE TABLE catalogos.OtroCargo (
        Id                    INT              NOT NULL IDENTITY(1,1),
        EmpresaId             INT              NOT NULL,
        Codigo                NVARCHAR(20)     NOT NULL,
        Nombre                NVARCHAR(150)    NOT NULL,
        TipoCargo             NVARCHAR(50)     NOT NULL DEFAULT 'Otro',
        AplicaIva             BIT              NOT NULL DEFAULT 0,
        TipoImpuestoId        INT              NULL,
        OrdenVisual           INT              NOT NULL DEFAULT 0,
        Activo                BIT              NOT NULL DEFAULT 1,

        -- Auditoría (AuditableEntity)
        FechaCreacion         DATETIME         NOT NULL DEFAULT GETDATE(),
        FechaModificacion     DATETIME         NULL,
        UsuarioCreacionId     UNIQUEIDENTIFIER NOT NULL,
        UsuarioModificacionId UNIQUEIDENTIFIER NULL,
        Borrado               BIT              NOT NULL DEFAULT 0,
        RowVersion            ROWVERSION       NOT NULL,

        CONSTRAINT PK_OtroCargo PRIMARY KEY (Id),
        CONSTRAINT FK_OtroCargo_Empresa FOREIGN KEY (EmpresaId)
            REFERENCES core.Empresa (Id),
        CONSTRAINT FK_OtroCargo_TipoImpuesto FOREIGN KEY (TipoImpuestoId)
            REFERENCES catalogos.TipoImpuesto (Id)
    );

    CREATE UNIQUE INDEX UQ_OtroCargo_EmpresaCodigo
        ON catalogos.OtroCargo (EmpresaId, Codigo)
        WHERE Borrado = 0;

    CREATE INDEX IX_OtroCargo_EmpresaId
        ON catalogos.OtroCargo (EmpresaId);

    PRINT 'Tabla catalogos.OtroCargo creada.';
END
ELSE
    PRINT 'Tabla catalogos.OtroCargo ya existe — omitida.';

-- ── Datos seed iniciales de ParametroGlobal ────────────────────────────────
-- Ejecutar SOLO si la empresa 1 existe y no tiene parámetros aún.
-- Ajustar EmpresaId y UsuarioCreacionId según ambiente.

/*
DECLARE @EmpresaId INT = 1;
DECLARE @UsrId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001'; -- reemplazar con ID real

IF NOT EXISTS (SELECT 1 FROM catalogos.ParametroGlobal WHERE EmpresaId = @EmpresaId)
BEGIN
    INSERT INTO catalogos.ParametroGlobal
        (EmpresaId, Clave, Valor, Descripcion, TipoDato, Grupo, OrdenVisual, Activo, FechaCreacion, UsuarioCreacionId)
    VALUES
        (@EmpresaId, 'iva.tasa.default',         '0.16',  'Tasa estándar de IVA (0.16 = 16%)',        'decimal', 'Fiscal',     1, 1, GETDATE(), @UsrId),
        (@EmpresaId, 'moneda.codigo.default',     'MXN',   'Código ISO de moneda base',                 'string',  'Moneda',     1, 1, GETDATE(), @UsrId),
        (@EmpresaId, 'moneda.simbolo.default',    '$',     'Símbolo monetario para display',             'string',  'Moneda',     2, 1, GETDATE(), @UsrId),
        (@EmpresaId, 'decimal.monetario.digitos', '2',     'Número de decimales en valores monetarios',  'int',     'Moneda',     3, 1, GETDATE(), @UsrId),
        (@EmpresaId, 'cotizacion.vigencia.dias',  '30',    'Días de vigencia default para cotizaciones', 'int',     'Documentos', 1, 1, GETDATE(), @UsrId),
        (@EmpresaId, 'serie.folio.default',       'A',     'Serie de folios para documentos comerciales','string',  'Documentos', 2, 1, GETDATE(), @UsrId);

    PRINT 'Datos seed de ParametroGlobal insertados.';
END
ELSE
    PRINT 'Empresa ya tiene parámetros — seed omitido.';
*/

PRINT '== AddConfigTables_V1.sql completado ==';
