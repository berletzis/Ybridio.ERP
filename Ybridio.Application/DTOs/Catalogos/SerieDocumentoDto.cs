using Ybridio.Domain.Catalogos;

namespace Ybridio.Application.DTOs.Catalogos;

/// <summary>
/// DTO de lectura para SerieDocumento.
/// FolioSiguiente es la representación visual del próximo folio a generar (calculado, no persiste).
/// </summary>
public sealed record SerieDocumentoDto(
    int                  Id,
    int                  EmpresaId,
    int?                 SucursalId,
    string?              SucursalNombre,
    TipoDocumentoSerie   TipoDocumento,
    string               TipoDocumentoNombre,
    string               Prefijo,
    int                  Longitud,
    long                 SiguienteNumero,
    string               FolioSiguiente,       // computed: "COT-000001"
    bool                 ReinicioAnual,
    bool                 Activo
);

/// <summary>DTO para crear o actualizar una SerieDocumento.</summary>
public sealed record GuardarSerieDocumentoDto(
    int                Id,
    int?               SucursalId,
    TipoDocumentoSerie TipoDocumento,
    string             Prefijo,
    int                Longitud,
    long               SiguienteNumero,
    bool               ReinicioAnual,
    bool               Activo
);
