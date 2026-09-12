using Clients.Domain;
using Microsoft.EntityFrameworkCore;

namespace Clients.Infrastructure;

public sealed class ClientsDbContext(DbContextOptions<ClientsDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var c = model.Entity<Cliente>();
        c.ToTable("Clientes");
        c.HasKey(x => x.Id);
        c.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        c.Property(x => x.Genero).HasMaxLength(30).IsRequired();
        c.Property(x => x.Identificacion).HasMaxLength(30).IsRequired();
        c.HasIndex(x => x.Identificacion).IsUnique();
        c.Property(x => x.Direccion).HasMaxLength(250).IsRequired();
        c.Property(x => x.Telefono).HasMaxLength(30).IsRequired();
        c.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        c.Property(x => x.Version).IsConcurrencyToken();
        var o = model.Entity<OutboxMessage>();
        o.ToTable("Outbox");
        o.HasKey(x => x.Id);
        o.Property(x => x.Payload).IsRequired();
        o.HasIndex(x => new { x.PublishedAt, x.CreatedAt });
    }
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}
