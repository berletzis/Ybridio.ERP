namespace Ybridio.Domain.Finanzas;

/// <summary>
/// Contexto al que pertenece un movimiento financiero.
/// Permite que la misma plataforma opere para empresas, sucursales y, en el futuro, usuarios personales o familias.
/// </summary>
public enum ContextoFinanciero
{
    /// <summary>Movimiento a nivel empresa (gastos/ingresos corporativos).</summary>
    Empresa = 0,

    /// <summary>Movimiento específico de una sucursal.</summary>
    Sucursal = 1,

    /// <summary>Movimiento personal de un usuario (finanzas personales).</summary>
    Usuario = 2,

    /// <summary>Movimiento de unidad familiar (uso futuro).</summary>
    Familia = 3,
}
