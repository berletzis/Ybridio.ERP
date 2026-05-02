using Ybridio.Domain.Common;

namespace Ybridio.Domain.Finanzas;

public class AperturaCaja : CreationAuditEntity
{
    public int Id { get; set; }
    public int CajaId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime FechaApertura { get; set; }
    public decimal MontoInicial { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal? MontoFinal { get; set; }
    public bool Activa { get; set; }

    // Sobrescribe la base — nullable porque apertura puede crearse sin usuario de auditoría
    public new Guid? UsuarioCreacionId { get; set; }

    // Navegación
    public Caja Caja { get; set; } = null!;
}
