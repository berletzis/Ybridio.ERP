using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

internal sealed class FacturaConfiguration : IEntityTypeConfiguration<Factura>
{
    public void Configure(EntityTypeBuilder<Factura> builder)
    {
        builder.ToTable("Factura", "ventas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Fecha).IsRequired();
        builder.Property(e => e.Total).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.UUID).HasMaxLength(100);

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId);

        builder.HasOne(e => e.Cliente)
            .WithMany()
            .HasForeignKey(e => e.ClienteId);

        builder.HasOne(e => e.Venta)
            .WithMany(e => e.Facturas)
            .HasForeignKey(e => e.VentaId)
            .IsRequired(false);
    }
}
