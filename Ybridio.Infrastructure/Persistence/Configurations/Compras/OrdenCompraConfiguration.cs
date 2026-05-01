using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Compras;

namespace Ybridio.Infrastructure.Persistence.Configurations.Compras;

internal sealed class OrdenCompraConfiguration : IEntityTypeConfiguration<OrdenCompra>
{
    public void Configure(EntityTypeBuilder<OrdenCompra> builder)
    {
        builder.ToTable("OrdenCompra", "compras");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Total).IsRequired().HasColumnType("decimal(18,6)");
        builder.Property(e => e.Estatus).IsRequired();
        builder.Property(e => e.Fecha).IsRequired();

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId);

        builder.HasOne(e => e.Proveedor)
            .WithMany()
            .HasForeignKey(e => e.ProveedorId);

        builder.HasIndex(e => new { e.EmpresaId, e.ProveedorId, e.Fecha })
            .HasDatabaseName("IX_OrdenCompra_EmpresaId_ProveedorId_Fecha");
    }
}
