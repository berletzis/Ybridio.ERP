using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

/// <summary>
/// Configuración EF Core para <see cref="CotizacionCargo"/>.
/// Tabla: ventas.CotizacionCargo — cargos accesorios de cotizaciones (Commercial Charges Pattern).
/// </summary>
internal sealed class CotizacionCargoConfiguration : IEntityTypeConfiguration<CotizacionCargo>
{
    public void Configure(EntityTypeBuilder<CotizacionCargo> builder)
    {
        builder.ToTable("CotizacionCargo", "ventas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Descripcion).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Importe).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(e => e.AplicaIva).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.Orden).IsRequired().HasDefaultValue(0);

        builder.HasOne(e => e.Cotizacion)
            .WithMany(c => c.Cargos)
            .HasForeignKey(e => e.CotizacionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.OtroCargo)
            .WithMany()
            .HasForeignKey(e => e.OtroCargoId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
