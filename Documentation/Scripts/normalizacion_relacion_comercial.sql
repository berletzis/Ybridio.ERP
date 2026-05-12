-- ============================================================
-- ⚠️  OBSOLETO — ANTI-PATTERN según ADR-038 (2026-05-11)
-- ============================================================
-- Este script fue creado bajo ADR-037 para normalización preventiva masiva
-- de RelacionComercial.
--
-- ADR-038 prohíbe explícitamente este enfoque:
--   • RelacionComercial NO es catálogo maestro — es vínculo transaccional.
--   • La normalización preventiva genera relaciones "fantasma".
--   • El selector consume IDirectorioService → Persona + EmpresaComercial directamente.
--   • RelacionComercial se crea BAJO DEMANDA al guardar el primer documento real
--     mediante GetOrCreateRelacionComercialAsync().
--
-- ❌ NO EJECUTAR este script.
-- ✅ Usar el patrón GetOrCreateAsync en la capa Application.
-- 📄 Referencia: Documentation/DECISIONS.md → ADR-038
-- ============================================================

-- [SCRIPT DESHABILITADO POR ADR-038 — contenido original preservado solo como referencia histórica]
/*
-- CONFIGURAR ANTES DE EJECUTAR (OBSOLETO):
DECLARE @UsuarioCreacionId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001'; -- ← Reemplazar con GUID real
DECLARE @FechaCreacion     DATETIME2        = GETDATE();

BEGIN TRANSACTION;
BEGIN TRY

	-- ── 1. EmpresasComerciales sin RelacionComercial ──────────────────────
	-- TipoRelacion = 2 (Cliente) como valor inicial razonable
	-- Ajustar manualmente si se requiere Prospecto (1), Proveedor (3) o Mixto (4)

	INSERT INTO core.RelacionComercial
		(EmpresaId, PersonaId, EmpresaComercialId, TipoRelacion,
		 LimiteCredito, Activo, Observaciones,
		 FechaCreacion, UsuarioCreacionId, Borrado)
	SELECT
		ec.EmpresaId,
		NULL,               -- PersonaId
		ec.Id,              -- EmpresaComercialId
		2,                  -- TipoRelacion = Cliente
		0,                  -- LimiteCredito = contado
		1,                  -- Activo = true
		'Normalizado automáticamente desde EmpresaComercial existente (ADR-037)',
		@FechaCreacion,
		@UsuarioCreacionId,
		0                   -- Borrado = false
	FROM core.EmpresaComercial ec
	WHERE ec.Borrado = 0
	  AND NOT EXISTS (
		  SELECT 1 FROM core.RelacionComercial rc
		  WHERE rc.EmpresaComercialId = ec.Id
			AND rc.Borrado = 0
	  );

	PRINT CONCAT('EmpresasComerciales normalizadas: ', @@ROWCOUNT);

	-- ── 2. Personas sin RelacionComercial ─────────────────────────────────
	-- TipoRelacion = 1 (Prospecto) como valor inicial conservador
	-- Las personas sin historial de transacciones son prospectos

	INSERT INTO core.RelacionComercial
		(EmpresaId, PersonaId, EmpresaComercialId, TipoRelacion,
		 LimiteCredito, Activo, Observaciones,
		 FechaCreacion, UsuarioCreacionId, Borrado)
	SELECT
		p.EmpresaId,
		p.Id,               -- PersonaId
		NULL,               -- EmpresaComercialId
		1,                  -- TipoRelacion = Prospecto
		0,                  -- LimiteCredito = contado
		1,                  -- Activo = true
		'Normalizado automáticamente desde Persona existente (ADR-037)',
		@FechaCreacion,
		@UsuarioCreacionId,
		0                   -- Borrado = false
	FROM core.Persona p
	WHERE p.Borrado = 0
	  AND NOT EXISTS (
		  SELECT 1 FROM core.RelacionComercial rc
		  WHERE rc.PersonaId = p.Id
			AND rc.Borrado = 0
	  );

	PRINT CONCAT('Personas normalizadas: ', @@ROWCOUNT);

	COMMIT TRANSACTION;
	PRINT 'Normalización completada exitosamente.';

END TRY
BEGIN CATCH
	ROLLBACK TRANSACTION;
	PRINT CONCAT('ERROR: ', ERROR_MESSAGE());
	THROW;
END CATCH;

-- Verificar resultado
SELECT
	rc.Id,
	rc.EmpresaId,
	rc.PersonaId,
	rc.EmpresaComercialId,
	rc.TipoRelacion,
	rc.Observaciones,
	rc.FechaCreacion
FROM core.RelacionComercial rc
WHERE rc.Observaciones LIKE '%ADR-037%'
ORDER BY rc.EmpresaId, rc.Id;
*/ -- FIN BLOQUE HISTÓRICO DESHABILITADO (ADR-038)
