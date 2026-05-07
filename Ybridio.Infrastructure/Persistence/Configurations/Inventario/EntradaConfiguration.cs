using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Inventario;

namespace Ybridio.Infrastructure.Persistence.Configurations.Inventario;

internal sealed class EntradaConfiguration : IEntityTypeConfiguration<Entrada>
{
    public void Configure(EntityTypeBuilder<Entrada> builder)
    {
        builder.ToTable("Entrada", "inventario");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Folio).HasMaxLength(50);
        builder.Property(e => e.Fecha).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.ReferenciaExterna).HasMaxLength(150);
        builder.Property(e => e.NumeroFactura).HasMaxLength(100);
        builder.Property(e => e.Subtotal).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.TotalImpuestos).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.Total).IsRequired().HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.Observaciones).HasMaxLength(1000);
        builder.Property(e => e.Aplicada).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => new { e.EmpresaId, e.SucursalId }).HasDatabaseName("IX_Entrada_EmpresaSucursal");
        builder.HasIndex(e => e.AlmacenId).HasDatabaseName("IX_Entrada_Almacen");
        builder.HasIndex(e => e.Folio).HasDatabaseName("IX_Entrada_Folio").HasFilter("[Folio] IS NOT NULL");

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId).HasConstraintName("FK_Entrada_Empresa");
        builder.HasOne(e => e.Sucursal).WithMany().HasForeignKey(e => e.SucursalId).HasConstraintName("FK_Entrada_Sucursal");
        builder.HasOne(e => e.Almacen).WithMany().HasForeignKey(e => e.AlmacenId).HasConstraintName("FK_Entrada_Almacen");
        builder.HasOne(e => e.AlmacenOrigen).WithMany().HasForeignKey(e => e.AlmacenOrigenId)
            .HasConstraintName("FK_Entrada_AlmacenOrigen").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.ConceptoEntrada).WithMany(c => c.Entradas).HasForeignKey(e => e.ConceptoEntradaId)
            .HasConstraintName("FK_Entrada_ConceptoEntrada");
        builder.HasOne(e => e.EstatusEntrada).WithMany(s => s.Entradas).HasForeignKey(e => e.EstatusEntradaId)
            .HasConstraintName("FK_Entrada_EstatusEntrada");
        builder.HasOne(e => e.Proveedor).WithMany().HasForeignKey(e => e.ProveedorId)
            .HasConstraintName("FK_Entrada_Proveedor").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.OrdenCompra).WithMany().HasForeignKey(e => e.OrdenCompraId)
            .HasConstraintName("FK_Entrada_OrdenCompra").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.SalidaOrigen).WithMany().HasForeignKey(e => e.SalidaOrigenId)
            .HasConstraintName("FK_Entrada_SalidaOrigen").OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Detalles).WithOne(d => d.Entrada).HasForeignKey(d => d.EntradaId);
    }
}
