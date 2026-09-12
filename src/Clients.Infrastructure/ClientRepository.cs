using System.Security.Cryptography;
using System.Text.Json;
using Bank.Contracts;
using Clients.Application;
using Clients.Domain;
using Microsoft.EntityFrameworkCore;

namespace Clients.Infrastructure;

public sealed class ClientRepository(ClientsDbContext db) : IClientRepository
{
    public Task<Cliente?> Find(Guid id, CancellationToken ct) => db.Clientes.SingleOrDefaultAsync(c => c.Id == id && !c.Eliminado, ct);
    public async Task<IReadOnlyList<Cliente>> List(int page, int size, CancellationToken ct) =>
        await db.Clientes.AsNoTracking().Where(c => !c.Eliminado).OrderBy(c => c.Nombre).ThenBy(c => c.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
    public void Add(Cliente cliente) => db.Clientes.Add(cliente);
    public async Task SaveWithEvent(Cliente cliente, CancellationToken ct)
    {
        var e = new ClientChanged(Guid.NewGuid(), cliente.Id, cliente.Nombre, cliente.Estado,
            cliente.Eliminado, cliente.Version, DateTime.UtcNow);
        db.Outbox.Add(new OutboxMessage { Id = e.EventId, Payload = JsonSerializer.Serialize(e), CreatedAt = e.OccurredAt });
        // EF saves the client and the event in one database transaction.
        await db.SaveChangesAsync(ct);
    }
}

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA512, 32);
        return $"PBKDF2-SHA512:210000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }
}
