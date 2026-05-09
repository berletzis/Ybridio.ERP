using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Finanzas;

/// <summary>
/// Catálogo de categorías para clasificar gastos e ingresos operativos.
/// Ejemplos: "Servicios básicos", "Nómina", "Gasolina", "Recuperación de cartera".
/// <para>
/// <see cref="TipoAplicable"/> limita qué tipo de movimientos pueden usar la categoría.
/// El valor "Ambos" permite usarla para gastos e ingresos indistintamente.
/// </para>
/// </summary>
public class CategoriaFinanciera : AuditableEntity
{
    public int    Id           { get; set; }
    public int    EmpresaId    { get; set; }

    /// <summary>"Gasto" | "Ingreso" | "Ambos" — almacenado como string para legibilidad en DB.</summary>
    public string TipoAplicable { get; set; } = "Ambos";

    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Color hexadecimal para identificación visual en la UI (e.g. "#E8A0BF").</summary>
    public string? Color       { get; set; }
    public bool    Activo      { get; set; } = true;

    // Navegación
    public Empresa Empresa { get; set; } = null!;
    public ICollection<MovimientoFinanciero> Movimientos { get; set; } = [];
}
