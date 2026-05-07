using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class SalidaConfiguration : IEntityTypeConfiguration<Salida>
{
    public void Configure(EntityTypeBuilder<Salida> builder)
    {
        builder.ToTable("Salida", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Folio).HasMaxLength(50);
        builder.Property(e => e.Fecha).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.Total).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.Observaciones).HasMaxLength(1000);
        builder.Property(e => e.Aplicada).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => new { e.EmpresaId, e.SucursalId }).HasDatabaseName("IX_Salida_EmpresaSucursal");
        builder.HasIndex(e => e.AlmacenId).HasDatabaseName("IX_Salida_Almacen");
        builder.HasIndex(e => e.Folio).HasDatabaseName("IX_Salida_Folio").HasFilter("[Folio] IS NOT NULL");

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId).HasConstraintName("FK_Salida_Empresa");
        builder.HasOne(e => e.Sucursal).WithMany().HasForeignKey(e => e.SucursalId).HasConstraintName("FK_Salida_Sucursal");
        builder.HasOne(e => e.Almacen).WithMany().HasForeignKey(e => e.AlmacenId).HasConstraintName("FK_Salida_Almacen");
        builder.HasOne(e => e.AlmacenDestino).WithMany().HasForeignKey(e => e.AlmacenDestinoId)
            .HasConstraintName("FK_Salida_AlmacenDestino").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.ConceptoSalida).WithMany(c => c.Salidas).HasForeignKey(e => e.ConceptoSalidaId)
            .HasConstraintName("FK_Salida_ConceptoSalida");
        builder.HasOne(e => e.EstatusSalida).WithMany(s => s.Salidas).HasForeignKey(e => e.EstatusSalidaId)
            .HasConstraintName("FK_Salida_EstatusSalida");
        builder.HasOne(e => e.Venta).WithMany().HasForeignKey(e => e.VentaId)
            .HasConstraintName("FK_Salida_Venta").OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Detalles).WithOne(d => d.Salida).HasForeignKey(d => d.SalidaId);
    }
}
