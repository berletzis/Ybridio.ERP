using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class AlmacenConfiguration : IEntityTypeConfiguration<Almacen>
{
    public void Configure(EntityTypeBuilder<Almacen> builder)
    {
        builder.ToTable("Almacen", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Codigo).HasMaxLength(50);
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(e => e.Descripcion).HasMaxLength(500);
        builder.Property(e => e.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.EsPrincipal).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Código único por sucursal (solo registros no borrados con código)
        builder.HasIndex(e => new { e.SucursalId, e.Codigo })
            .IsUnique()
            .HasDatabaseName("UQ_Almacen_Sucursal_Codigo")
            .HasFilter("[Codigo] IS NOT NULL AND [Borrado] = 0");

        builder.HasIndex(e => new { e.EmpresaId, e.SucursalId })
            .HasDatabaseName("IX_Almacen_EmpresaActivo")
            .HasFilter("[Borrado] = 0 AND [Activo] = 1");

        builder.HasOne(e => e.Empresa)
            .WithMany()
            .HasForeignKey(e => e.EmpresaId)
            .HasConstraintName("FK_Almacen_Empresa");

        builder.HasOne(e => e.Sucursal)
            .WithMany()
            .HasForeignKey(e => e.SucursalId)
            .HasConstraintName("FK_Almacen_Sucursal");
    }
}
