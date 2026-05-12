-- ============================================================
-- DIAGNÓSTICO: RelacionComercial — Datos Huérfanos
-- Ybridio ERP — ADR-036 / ADR-037
-- Ejecutar en: YBRIDIO-26 (base de datos de desarrollo)
-- Fecha: 2026
-- ============================================================
-- Este script identifica:
--   1. EmpresasComerciales sin RelacionComercial correspondiente
--   2. Personas sin RelacionComercial correspondiente
--   3. RelacionComerciales con referencias rotas (PersonaId/EmpresaComercialId apunta a nulo)
--   4. RelacionComerciales con ambas FKs nulas (invariante violada)
--   5. RelacionComerciales con ambas FKs populadas (invariante violada)
-- ============================================================

PRINT '== 1. EmpresasComerciales SIN RelacionComercial =='
SELECT
	ec.Id          AS EmpresaComercialId,
	ec.EmpresaId,
	ec.RazonSocial,
	ec.NombreComercial,
	ec.RFC,
	ec.Activo,
	ec.Borrado
FROM core.EmpresaComercial ec
WHERE ec.Borrado = 0
  AND NOT EXISTS (
	  SELECT 1 FROM core.RelacionComercial rc
	  WHERE rc.EmpresaComercialId = ec.Id
		AND rc.Borrado = 0
  )
ORDER BY ec.EmpresaId, ec.RazonSocial;

PRINT ''
PRINT '== 2. Personas SIN RelacionComercial =='
SELECT
	p.Id          AS PersonaId,
	p.EmpresaId,
	p.Nombre,
	p.Apellidos,
	p.RFC,
	p.Activo,
	p.Borrado
FROM core.Persona p
WHERE p.Borrado = 0
  AND NOT EXISTS (
	  SELECT 1 FROM core.RelacionComercial rc
	  WHERE rc.PersonaId = p.Id
		AND rc.Borrado = 0
  )
ORDER BY p.EmpresaId, p.Nombre;

PRINT ''
PRINT '== 3. RelacionComerciales con referencia a Persona inexistente =='
SELECT
	rc.Id,
	rc.EmpresaId,
	rc.PersonaId,
	rc.TipoRelacion,
	rc.Activo
FROM core.RelacionComercial rc
WHERE rc.Borrado = 0
  AND rc.PersonaId IS NOT NULL
  AND NOT EXISTS (
	  SELECT 1 FROM core.Persona p WHERE p.Id = rc.PersonaId
  );

PRINT ''
PRINT '== 4. RelacionComerciales con referencia a EmpresaComercial inexistente =='
SELECT
	rc.Id,
	rc.EmpresaId,
	rc.EmpresaComercialId,
	rc.TipoRelacion,
	rc.Activo
FROM core.RelacionComercial rc
WHERE rc.Borrado = 0
  AND rc.EmpresaComercialId IS NOT NULL
  AND NOT EXISTS (
	  SELECT 1 FROM core.EmpresaComercial ec WHERE ec.Id = rc.EmpresaComercialId
  );

PRINT ''
PRINT '== 5. RelacionComerciales con AMBAS FKs nulas (invariante violada) =='
SELECT
	rc.Id,
	rc.EmpresaId,
	rc.TipoRelacion,
	rc.Activo,
	rc.FechaCreacion
FROM core.RelacionComercial rc
WHERE rc.Borrado = 0
  AND rc.PersonaId IS NULL
  AND rc.EmpresaComercialId IS NULL;

PRINT ''
PRINT '== 6. RelacionComerciales con AMBAS FKs populadas (invariante violada) =='
SELECT
	rc.Id,
	rc.EmpresaId,
	rc.PersonaId,
	rc.EmpresaComercialId,
	rc.TipoRelacion,
	rc.Activo
FROM core.RelacionComercial rc
WHERE rc.Borrado = 0
  AND rc.PersonaId IS NOT NULL
  AND rc.EmpresaComercialId IS NOT NULL;

PRINT ''
PRINT '== 7. RESUMEN CONTEOS por EmpresaId =='
SELECT
	e.Id           AS EmpresaId,
	e.Nombre       AS Empresa,
	COUNT(DISTINCT rc.Id) AS TotalRelaciones,
	COUNT(DISTINCT CASE WHEN rc.PersonaId IS NOT NULL THEN rc.Id END) AS RelPersonas,
	COUNT(DISTINCT CASE WHEN rc.EmpresaComercialId IS NOT NULL THEN rc.Id END) AS RelEmpresas,
	COUNT(DISTINCT ec.Id) AS TotalEmpresasComerciales,
	COUNT(DISTINCT p.Id)  AS TotalPersonas
FROM core.Empresa e
LEFT JOIN core.RelacionComercial rc ON rc.EmpresaId = e.Id AND rc.Borrado = 0
LEFT JOIN core.EmpresaComercial  ec ON ec.EmpresaId = e.Id AND ec.Borrado = 0
LEFT JOIN core.Persona            p ON p.EmpresaId  = e.Id AND p.Borrado  = 0
GROUP BY e.Id, e.Nombre
ORDER BY e.Id;
