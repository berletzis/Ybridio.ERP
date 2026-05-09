using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Finanzas;

public class CuentaPorCobrarConfiguration : IEntityTypeConfiguration<CuentaPorCobrar>
{
    public void Configure(EntityTypeBuilder<CuentaPorCobrar> builder)
    {
        builder.ToTable("CuentaPorCobrar", "finanzas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.NombreDeudor).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Concepto).IsRequired().HasMaxLength(200);
        builder.Property(e => e.MontoOriginal).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(e => e.MontoPagado).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.FechaEmision).IsRequired();
        builder.Property(e => e.FechaVencimiento).IsRequired();
        builder.Property(e => e.Observaciones).HasMaxLength(500);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.FechaModificacion);
        builder.Property(e => e.UsuarioModificacionId);
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Sucursal).WithMany().HasForeignKey(e => e.SucursalId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.EmpresaId, e.FechaVencimiento }).HasDatabaseName("IX_CuentaPorCobrar_Empresa_Vencimiento");
    }
}
