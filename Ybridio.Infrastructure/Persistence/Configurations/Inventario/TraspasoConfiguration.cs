using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class TraspasoConfiguration : IEntityTypeConfiguration<Traspaso>
{
    public void Configure(EntityTypeBuilder<Traspaso> builder)
    {
        builder.ToTable("Traspaso", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Folio).HasMaxLength(50);
        builder.Property(e => e.Fecha).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.Estatus).IsRequired().HasDefaultValue(1);
        builder.Property(e => e.Observaciones).HasMaxLength(1000);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => e.EmpresaId).HasDatabaseName("IX_Traspaso_Empresa");

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId).HasConstraintName("FK_Traspaso_Empresa");
        builder.HasOne(e => e.AlmacenOrigen).WithMany().HasForeignKey(e => e.AlmacenOrigenId)
            .HasConstraintName("FK_Traspaso_AlmacenOrigen").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.AlmacenDestino).WithMany().HasForeignKey(e => e.AlmacenDestinoId)
            .HasConstraintName("FK_Traspaso_AlmacenDestino").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Salida).WithMany().HasForeignKey(e => e.SalidaId)
            .HasConstraintName("FK_Traspaso_Salida").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Entrada).WithMany().HasForeignKey(e => e.EntradaId)
            .HasConstraintName("FK_Traspaso_Entrada").OnDelete(DeleteBehavior.NoAction);
    }
}
