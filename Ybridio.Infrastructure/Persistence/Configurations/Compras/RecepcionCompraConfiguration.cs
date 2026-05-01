using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Compras;

namespace Ybridio.Infrastructure.Persistence.Configurations.Compras;

internal sealed class RecepcionCompraConfiguration : IEntityTypeConfiguration<RecepcionCompra>
{
    public void Configure(EntityTypeBuilder<RecepcionCompra> builder)
    {
        builder.ToTable("RecepcionCompra", "compras");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Fecha).IsRequired();

        builder.Property(e => e.FechaCreacion).IsRequired()
            .HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId);

        builder.HasOne(e => e.OrdenCompra)
            .WithMany(e => e.Recepciones)
            .HasForeignKey(e => e.OrdenCompraId);
    }
}
