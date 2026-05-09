using Ybridio.Domain.Common;
using Ybridio.Domain.Core;

namespace Ybridio.Domain.Finanzas;

/// <summary>
/// Registro de un gasto o ingreso operativo no proveniente de ventas.
/// La discriminación entre gasto e ingreso se hace mediante <see cref="Tipo"/>.
/// <para>
/// El contexto financiero (<see cref="Contexto"/>) determina si el movimiento pertenece
/// a la empresa, sucursal o a un usuario específico (finanzas personales).
/// </para>
/// </summary>
public class MovimientoFinanciero : AuditableEntity
{
    public long   Id           { get; set; }
    public int    EmpresaId    { get; set; }

    /// <summary>Sucursal específica; null = movimiento a nivel empresa.</summary>
    public int?   SucursalId   { get; set; }

    public TipoMovimientoFinanciero Tipo     { get; set; }
    public ContextoFinanciero       Contexto { get; set; } = ContextoFinanciero.Empresa;

    /// <summary>Usuario propietario cuando Contexto = Usuario (finanzas personales).</summary>
    public Guid?  UsuarioContextoId { get; set; }

    public int?   CategoriaId  { get; set; }

    /// <summary>Descripción breve del movimiento (p.ej. "Pago servicio eléctrico agosto").</summary>
    public string  Concepto     { get; set; } = string.Empty;
    public decimal Monto        { get; set; }
    public DateTime Fecha       { get; set; }
    public string?  Observaciones { get; set; }

    // Navegación
    public Empresa            Empresa   { get; set; } = null!;
    public Sucursal?          Sucursal  { get; set; }
    public CategoriaFinanciera? Categoria { get; set; }
}
