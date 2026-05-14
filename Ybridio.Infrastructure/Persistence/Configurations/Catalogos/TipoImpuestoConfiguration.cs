using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Catalogos;

namespace Ybridio.Infrastructure.Persistence.Configurations.Catalogos;

/// <summary>
/// Configuración EF Core para <see cref="TipoImpuesto"/> — catálogo fiscal institucional.
/// Tabla: catalogos.TipoImpuesto
/// </summary>
internal sealed class TipoImpuestoConfiguration : IEntityTypeConfiguration<TipoImpuesto>
{
    public void Configure(EntityTypeBuilder<TipoImpuesto> builder)
    {
        builder.ToTable("TipoImpuesto", "catalogos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Codigo).IsRequired().HasMaxLength(20).HasDefaultValue(string.Empty);
        builder.Property(e => e.Porcentaje).IsRequired().HasColumnType("decimal(5,2)");
        builder.Property(e => e.TipoGravamen).IsRequired().HasConversion<int>().HasDefaultValue(TipoGravamen.IVA);
        builder.Property(e => e.EsExento).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.OrdenVisual).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_TipoImpuesto_Empresa");

        builder.HasIndex(e => new { e.EmpresaId, e.Codigo })
            .HasFilter("[Codigo] != ''")
            .IsUnique()
            .HasDatabaseName("UQ_TipoImpuesto_EmpresaCodigo");

        builder.HasIndex(e => e.EmpresaId).HasDatabaseName("IX_TipoImpuesto_EmpresaId");
    }
}
