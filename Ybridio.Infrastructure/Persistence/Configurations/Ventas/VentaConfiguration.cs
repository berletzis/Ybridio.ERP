using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

internal sealed class VentaConfiguration : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> builder)
    {
        builder.ToTable("Venta", "ventas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Fecha).IsRequired();
        builder.Property(e => e.Total).HasColumnType("decimal(18,2)");

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_Venta_Empresa");

        builder.HasOne(e => e.Sucursal)
            .WithMany()
            .HasForeignKey(e => e.SucursalId)
            .HasConstraintName("FK_Venta_Sucursal");

        builder.HasOne(e => e.Caja)
            .WithMany()
            .HasForeignKey(e => e.CajaId)
            .HasConstraintName("FK_Venta_Caja")
            .IsRequired(false);

        builder.HasOne(e => e.AperturaCaja)
            .WithMany()
            .HasForeignKey(e => e.AperturaCajaId)
            .HasConstraintName("FK_Venta_AperturaCaja")
            .IsRequired(false);

        builder.HasIndex(e => new { e.EmpresaId, e.SucursalId, e.Fecha })
            .HasDatabaseName("IX_Venta_EmpresaId_SucursalId_Fecha");
    }
}
