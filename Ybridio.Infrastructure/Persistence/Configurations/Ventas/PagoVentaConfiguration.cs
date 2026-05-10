using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

internal sealed class PagoVentaConfiguration : IEntityTypeConfiguration<PagoVenta>
{
    public void Configure(EntityTypeBuilder<PagoVenta> builder)
    {
        builder.ToTable("PagoVenta", "ventas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.Fecha).IsRequired();
        builder.Property(e => e.Monto).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.FormaPago).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Referencia).HasMaxLength(200).IsRequired(false);
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.VentaId).HasDatabaseName("IX_PagoVenta_VentaId");
    }
}
