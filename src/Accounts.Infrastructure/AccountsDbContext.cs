using Accounts.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accounts.Infrastructure;

public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : DbContext(options)
{
    public DbSet<Cuenta> Cuentas => Set<Cuenta>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();
    public DbSet<ClientSnapshot> Clients => Set<ClientSnapshot>();
    public DbSet<MovementAudit> Audits => Set<MovementAudit>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var c = model.Entity<Cuenta>();
        c.ToTable("Cuentas", t => t.HasCheckConstraint("CK_Cuentas_Saldo", "\"SaldoDisponible\" >= 0 AND \"SaldoInicial\" >= 0"));
        c.HasKey(x => x.Id);
        c.Property(x => x.NumeroCuenta).HasMaxLength(30).IsRequired();
        c.HasIndex(x => x.NumeroCuenta).IsUnique();
        c.Property(x => x.TipoCuenta).HasMaxLength(20).IsRequired();
        c.Property(x => x.SaldoInicial).HasPrecision(18, 2);
        c.Property(x => x.SaldoDisponible).HasPrecision(18, 2);
        c.Property(x => x.Version).IsConcurrencyToken();
        c.HasOne<ClientSnapshot>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        var m = model.Entity<Movimiento>();
        m.ToTable("Movimientos", t => t.HasCheckConstraint("CK_Movimientos_Importe", "\"Valor\" <> 0 AND \"Saldo\" >= 0"));
        m.HasKey(x => x.Id);
        m.HasOne<Cuenta>().WithMany().HasForeignKey(x => x.CuentaId).OnDelete(DeleteBehavior.Restrict);
        m.HasIndex(x => new { x.CuentaId, x.Fecha });
        m.HasIndex(x => new { x.CuentaId, x.Secuencia }).IsUnique();
        m.Property(x => x.TipoMovimiento).HasMaxLength(20).IsRequired();
        m.Property(x => x.Valor).HasPrecision(18, 2);
        m.Property(x => x.Saldo).HasPrecision(18, 2);
        var s = model.Entity<ClientSnapshot>();
        s.ToTable("ClientesProyeccion");
        s.HasKey(x => x.Id);
        s.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        s.Property(x => x.Version).IsConcurrencyToken();
        var a = model.Entity<MovementAudit>();
        a.ToTable("AuditoriaMovimientos");
        a.HasKey(x => x.Id);
        a.HasOne<Movimiento>().WithMany().HasForeignKey(x => x.MovimientoId).OnDelete(DeleteBehavior.Restrict);
        a.Property(x => x.ValorAnterior).HasPrecision(18, 2);
        a.Property(x => x.ValorNuevo).HasPrecision(18, 2);
    }
}
