namespace Ybridio.Domain.Common;

/// <summary>
/// Base para entidades con auditoría completa (creación + modificación + soft-delete + concurrencia).
/// </summary>
public abstract class AuditableEntity
{
    public DateTime FechaCreacion { get; set; }
    public Guid UsuarioCreacionId { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public Guid? UsuarioModificacionId { get; set; }
    public bool Borrado { get; set; }

    /// <summary>Concurrency token — mapeado al timestamp de SQL Server.</summary>
    public byte[] RowVersion { get; set; } = [];
}
