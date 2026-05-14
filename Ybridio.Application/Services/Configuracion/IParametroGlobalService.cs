using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ybridio.Application.Common;
using Ybridio.Application.DTOs.Catalogos;

namespace Ybridio.Application.Services.Configuracion;

/// <summary>
/// Gestiona los parámetros de configuración global del ERP para la empresa en sesión.
/// Parámetros son pares Clave-Valor que definen el comportamiento operacional:
/// tasas fiscales default, moneda base, vigencia de documentos, configuración de series, etc.
/// </summary>
public interface IParametroGlobalService
{
    /// <summary>Retorna todos los parámetros activos de la empresa, agrupados por Grupo.</summary>
    Task<IReadOnlyList<ParametroGlobalDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>Obtiene el valor de texto de un parámetro por clave. Retorna defaultValue si no existe.</summary>
    Task<string> ObtenerValorAsync(string clave, string defaultValue = "", CancellationToken ct = default);

    /// <summary>Obtiene el valor decimal de un parámetro por clave. Retorna defaultValue si no existe o no es parseable.</summary>
    Task<decimal> ObtenerDecimalAsync(string clave, decimal defaultValue = 0m, CancellationToken ct = default);

    /// <summary>Obtiene el valor entero de un parámetro por clave.</summary>
    Task<int> ObtenerIntAsync(string clave, int defaultValue = 0, CancellationToken ct = default);

    /// <summary>Obtiene el valor booleano de un parámetro por clave.</summary>
    Task<bool> ObtenerBoolAsync(string clave, bool defaultValue = false, CancellationToken ct = default);

    /// <summary>Crea o actualiza un parámetro. Usa Clave como identificador único por empresa.</summary>
    Task<ServiceResult<ParametroGlobalDto>> GuardarAsync(GuardarParametroGlobalDto dto, CancellationToken ct = default);

    /// <summary>Elimina lógicamente un parámetro (soft delete).</summary>
    Task<ServiceResult> EliminarAsync(int id, CancellationToken ct = default);
}
