using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

internal sealed class TipoProductoConfiguration : IEntityTypeConfiguration<TipoProducto>
{
    public void Configure(EntityTypeBuilder<TipoProducto> builder)
    {
        builder.ToTable("TipoProducto", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Clave).IsRequired().HasMaxLength(10).HasDefaultValue(string.Empty);
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.OrdenVisual).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_TipoProducto_Empresa");

        // Clave única por empresa (vacío excluido del índice)
        builder.HasIndex(e => new { e.EmpresaId, e.Clave })
            .HasFilter("[Clave] != ''")
            .IsUnique()
            .HasDatabaseName("UQ_TipoProducto_EmpresaClave");

        builder.HasIndex(e => e.EmpresaId).HasDatabaseName("IX_TipoProducto_EmpresaId");
    }
}
