using Accounts.Application;
using Accounts.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accounts.Infrastructure;

public sealed class AccountRepository(AccountsDbContext db) : IAccountRepository
{
    public Task<ClientSnapshot?> Client(Guid id, CancellationToken ct) => db.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
    public Task<Cuenta?> Account(Guid id, CancellationToken ct) => db.Cuentas.SingleOrDefaultAsync(c => c.Id == id, ct);
    public async Task<IReadOnlyList<Cuenta>> Accounts(Guid? cliente, int page, int size, CancellationToken ct) =>
        await db.Cuentas.AsNoTracking().Where(c => cliente == null || c.ClienteId == cliente)
            .OrderBy(c => c.NumeroCuenta).Skip((page - 1) * size).Take(size).ToListAsync(ct);
    public Task<Movimiento?> Movement(Guid id, CancellationToken ct) => db.Movimientos.SingleOrDefaultAsync(m => m.Id == id, ct);
    public Task<Movimiento?> Latest(Guid cuentaId, CancellationToken ct) => db.Movimientos.AsNoTracking()
        .Where(m => m.CuentaId == cuentaId).OrderByDescending(m => m.Secuencia).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<Movimiento>> Movements(Guid? cuenta, int page, int size, CancellationToken ct) =>
        await db.Movimientos.AsNoTracking().Where(m => cuenta == null || m.CuentaId == cuenta)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Secuencia).ThenBy(m => m.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
    public void Add(Cuenta c) => db.Cuentas.Add(c);
    public void Add(Movimiento m) => db.Movimientos.Add(m);
    public void Add(MovementAudit a) => db.Audits.Add(a);
    public async Task Save(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public async Task<Statement> Report(ClientSnapshot client, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var from = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var until = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        // A consistent snapshot across the account and movement queries.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var accounts = await db.Cuentas.AsNoTracking().Where(c => c.ClienteId == client.Id)
            .OrderBy(c => c.NumeroCuenta).ToListAsync(ct);
        var ids = accounts.Select(c => c.Id).ToArray();
        var movements = await db.Movimientos.AsNoTracking()
            .Where(m => ids.Contains(m.CuentaId) && m.Fecha >= from && m.Fecha < until)
            .OrderBy(m => m.Fecha).ThenBy(m => m.Secuencia).ToListAsync(ct);
        // Correlated indexed lookups obtain closing balances without loading the entire history.
        var closing = await db.Cuentas.AsNoTracking().Where(c => c.ClienteId == client.Id)
            .Select(c => new { c.Id, Saldo = db.Movimientos.Where(m => m.CuentaId == c.Id && m.Fecha < until)
                .OrderByDescending(m => m.Secuencia).Select(m => (decimal?)m.Saldo).FirstOrDefault() ?? c.SaldoInicial })
            .ToDictionaryAsync(x => x.Id, x => x.Saldo, ct);
        var opening = await db.Cuentas.AsNoTracking().Where(c => c.ClienteId == client.Id)
            .Select(c => new { c.Id, Saldo = db.Movimientos.Where(m => m.CuentaId == c.Id && m.Fecha < from)
                .OrderByDescending(m => m.Secuencia).Select(m => (decimal?)m.Saldo).FirstOrDefault() ?? c.SaldoInicial })
            .ToDictionaryAsync(x => x.Id, x => x.Saldo, ct);
        await tx.CommitAsync(ct);
        var byAccount = movements.ToLookup(m => m.CuentaId);
        return new Statement(client.Id, client.Nombre, desde, hasta, accounts.Select(c =>
            new StatementAccount(c.NumeroCuenta, c.TipoCuenta, c.Estado, opening[c.Id], closing[c.Id],
                byAccount[c.Id].Select(m => new StatementMovement(m.Id, m.Fecha, m.TipoMovimiento,
                    m.Saldo - m.Valor, m.Valor, m.Saldo)).ToList())).ToList());
    }
}
