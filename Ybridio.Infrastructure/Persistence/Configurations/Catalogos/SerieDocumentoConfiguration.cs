using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

/// <summary>
/// Configuración EF Core para <see cref="SerieDocumento"/>.
/// Tabla: catalogos.SerieDocumento — consecutivos documentales del ERP.
/// </summary>
internal sealed class SerieDocumentoConfiguration : IEntityTypeConfiguration<SerieDocumento>
{
    public void Configure(EntityTypeBuilder<SerieDocumento> builder)
    {
        builder.ToTable("SerieDocumento", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.TipoDocumento).IsRequired().HasConversion<int>();
        builder.Property(e => e.Prefijo).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Longitud).IsRequired().HasDefaultValue(6);
        builder.Property(e => e.SiguienteNumero).IsRequired().HasDefaultValue(1L);
        builder.Property(e => e.ReinicioAnual).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.AnioUltimoReinicio);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_SerieDocumento_Empresa");

        builder.HasOne(e => e.Sucursal).WithMany().HasForeignKey(e => e.SucursalId)
            .IsRequired(false)
            .HasConstraintName("FK_SerieDocumento_Sucursal");

        // Una empresa puede tener como máximo una serie activa por TipoDocumento + SucursalId
        // (null SucursalId = serie global de la empresa)
        builder.HasIndex(e => new { e.EmpresaId, e.TipoDocumento, e.SucursalId })
            .IsUnique()
            .HasDatabaseName("UQ_SerieDocumento_Empresa_Tipo_Sucursal");

        builder.HasIndex(e => e.EmpresaId)
            .HasDatabaseName("IX_SerieDocumento_EmpresaId");
    }
}
