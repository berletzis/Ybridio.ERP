using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ybridio.Domain.Catalogos;


namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;
// ── CategoriaProductoConfiguration.cs ────────────────────────────────────────
internal sealed class CategoriaProductoConfiguration : IEntityTypeConfiguration<CategoriaProducto>
{
    public void Configure(EntityTypeBuilder<CategoriaProducto> builder)
    {
        builder.ToTable("CategoriaProducto", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_CategoriaProducto_Empresa");

        builder.Property(e => e.CategoriaPadreId).IsRequired(false);
        builder.HasOne(e => e.CategoriaPadre)
            .WithMany(c => c.SubCategorias)
            .HasForeignKey(e => e.CategoriaPadreId)
            .HasConstraintName("FK_CategoriaProducto_Padre")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.EmpresaId).HasDatabaseName("IX_CategoriaProducto_EmpresaId");
    }
}