using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Ventas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Ventas;

public class CotizacionConfiguration : IEntityTypeConfiguration<Cotizacion>
{
    public void Configure(EntityTypeBuilder<Cotizacion> builder)
    {
        builder.ToTable("Cotizacion", "ventas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Folio).HasMaxLength(50);
        builder.HasIndex(e => new { e.EmpresaId, e.Folio })
            .HasFilter("[Folio] IS NOT NULL")
            .IsUnique()
            .HasDatabaseName("UQ_Cotizacion_EmpresaFolio");

        builder.Property(e => e.Estatus).IsRequired().HasConversion<int>();
        builder.Property(e => e.NombreCliente).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Fecha).IsRequired();
        builder.Property(e => e.FechaVigencia);
        builder.Property(e => e.VendedorId);
        builder.Property(e => e.Subtotal).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.Total).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.Observaciones).HasMaxLength(500);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.FechaModificacion);
        builder.Property(e => e.UsuarioModificacionId);
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Sucursal).WithMany().HasForeignKey(e => e.SucursalId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.RelacionComercial).WithMany().HasForeignKey(e => e.RelacionComercialId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.EmpresaId, e.Fecha }).HasDatabaseName("IX_Cotizacion_Empresa_Fecha");
        builder.HasIndex(e => new { e.EmpresaId, e.Estatus }).HasDatabaseName("IX_Cotizacion_Empresa_Estatus");
    }
}
