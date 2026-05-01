namespace Ybridio.Domain.Common;

/// <summary>
/// Base para entidades con auditoría solo de creación y soft-delete (sin modificación).
/// </summary>
public abstract class CreationAuditEntity
{
    public DateTime FechaCreacion { get; set; }
    public Guid UsuarioCreacionId { get; set; }
    public bool Borrado { get; set; }

    /// <summary>Concurrency token — mapeado al timestamp de SQL Server.</summary>
    public byte[] RowVersion { get; set; } = [];
}
