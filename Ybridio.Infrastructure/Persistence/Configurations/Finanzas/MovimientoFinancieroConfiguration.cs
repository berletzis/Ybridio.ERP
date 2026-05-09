using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ybridio.Domain.Finanzas;

namespace Ybridio.Infrastructure.Persistence.Configurations.Finanzas;

public class MovimientoFinancieroConfiguration : IEntityTypeConfiguration<MovimientoFinanciero>
{
    public void Configure(EntityTypeBuilder<MovimientoFinanciero> builder)
    {
        builder.ToTable("MovimientoFinanciero", "finanzas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Tipo).IsRequired().HasConversion<int>();
        builder.Property(e => e.Contexto).IsRequired().HasConversion<int>().HasDefaultValue(ContextoFinanciero.Empresa);
        builder.Property(e => e.UsuarioContextoId);
        builder.Property(e => e.Concepto).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Monto).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(e => e.Fecha).IsRequired();
        builder.Property(e => e.Observaciones).HasMaxLength(500);

        builder.Property(e => e.FechaCreacion).IsRequired().HasDefaultValueSql("getdate()");
        builder.Property(e => e.UsuarioCreacionId).IsRequired();
        builder.Property(e => e.FechaModificacion);
        builder.Property(e => e.UsuarioModificacionId);
        builder.Property(e => e.Borrado).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasOne(e => e.Empresa).WithMany().HasForeignKey(e => e.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Sucursal).WithMany().HasForeignKey(e => e.SucursalId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Categoria).WithMany(c => c.Movimientos).HasForeignKey(e => e.CategoriaId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.EmpresaId, e.Tipo, e.Fecha }).HasDatabaseName("IX_MovimientoFinanciero_Empresa_Tipo_Fecha");
        builder.HasIndex(e => new { e.EmpresaId, e.SucursalId, e.Fecha }).HasDatabaseName("IX_MovimientoFinanciero_Empresa_Sucursal_Fecha");
    }
}
