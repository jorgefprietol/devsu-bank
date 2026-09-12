using Accounts.Domain;
using Bank.Contracts;

namespace Accounts.Application;

public sealed record AccountInput(Guid ClienteId, string NumeroCuenta, string TipoCuenta,
    decimal SaldoInicial, bool Estado = true);
public sealed record AccountUpdate(string TipoCuenta, bool Estado);
public sealed record AccountPatch(string? TipoCuenta, bool? Estado);
public sealed record MovementInput(Guid CuentaId, decimal Valor, DateTimeOffset? Fecha = null);
public sealed record MovementUpdate(decimal Valor);
public sealed record AccountView(Guid CuentaId, Guid ClienteId, string NumeroCuenta, string TipoCuenta,
    decimal SaldoInicial, decimal SaldoDisponible, bool Estado)
{
    public static AccountView From(Cuenta c) => new(c.Id, c.ClienteId, c.NumeroCuenta,
        c.TipoCuenta, c.SaldoInicial, c.SaldoDisponible, c.Estado);
}
public sealed record MovementView(Guid MovimientoId, Guid CuentaId, DateTime Fecha, string TipoMovimiento,
    decimal Valor, decimal Saldo, long Secuencia)
{
    public static MovementView From(Movimiento m) => new(m.Id, m.CuentaId, m.Fecha, m.TipoMovimiento,
        m.Valor, m.Saldo, m.Secuencia);
}
public sealed record Statement(Guid ClienteId, string Cliente, DateOnly Desde, DateOnly Hasta,
    IReadOnlyList<StatementAccount> Cuentas);
public sealed record StatementAccount(string NumeroCuenta, string Tipo, bool Estado,
    decimal SaldoInicial, decimal SaldoDisponible, IReadOnlyList<StatementMovement> Movimientos);
public sealed record StatementMovement(Guid MovimientoId, DateTime Fecha, string TipoMovimiento,
    decimal SaldoInicial, decimal Movimiento, decimal SaldoDisponible);

public interface IAccountRepository
{
    Task<ClientSnapshot?> Client(Guid id, CancellationToken ct);
    Task<Cuenta?> Account(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Cuenta>> Accounts(Guid? cliente, int page, int size, CancellationToken ct);
    Task<Movimiento?> Movement(Guid id, CancellationToken ct);
    Task<Movimiento?> Latest(Guid cuentaId, CancellationToken ct);
    Task<IReadOnlyList<Movimiento>> Movements(Guid? cuenta, int page, int size, CancellationToken ct);
    Task<Statement> Report(ClientSnapshot client, DateOnly desde, DateOnly hasta, CancellationToken ct);
    void Add(Cuenta cuenta);
    void Add(Movimiento movimiento);
    void Add(MovementAudit audit);
    Task Save(CancellationToken ct);
}

public sealed class AccountService(IAccountRepository repository)
{
    public async Task<AccountView> Get(Guid id, CancellationToken ct) => AccountView.From(await FindAccount(id, ct));
    public async Task<IReadOnlyList<AccountView>> List(Guid? cliente, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        return (await repository.Accounts(cliente, page, size, ct)).Select(AccountView.From).ToList();
    }
    public async Task<AccountView> Create(AccountInput input, CancellationToken ct)
    {
        await ActiveClient(input.ClienteId, ct);
        var c = Cuenta.Create(input.ClienteId, input.NumeroCuenta, input.TipoCuenta, input.SaldoInicial, input.Estado);
        repository.Add(c);
        await repository.Save(ct);
        return AccountView.From(c);
    }
    public async Task<AccountView> Patch(Guid id, AccountPatch input, CancellationToken ct)
    {
        var c = await FindAccount(id, ct);
        c.Update(input.TipoCuenta ?? c.TipoCuenta, input.Estado ?? c.Estado);
        await repository.Save(ct);
        return AccountView.From(c);
    }
    public async Task<MovementView> GetMovement(Guid id, CancellationToken ct) => MovementView.From(await FindMovement(id, ct));
    public async Task<IReadOnlyList<MovementView>> Movements(Guid? cuenta, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        return (await repository.Movements(cuenta, page, size, ct)).Select(MovementView.From).ToList();
    }
    public async Task<MovementView> Register(MovementInput input, Guid? idempotencyKey, CancellationToken ct)
    {
        var id = idempotencyKey ?? Guid.NewGuid();
        if (id == Guid.Empty) throw new BusinessException("Idempotency-Key debe ser un UUID no vacío.");
        var existing = await repository.Movement(id, ct);
        if (existing is not null)
        {
            if (existing.CuentaId != input.CuentaId || existing.Valor != input.Valor ||
                (input.Fecha.HasValue && existing.Fecha != input.Fecha.Value.UtcDateTime))
                throw new BusinessException("Idempotency-Key ya utilizado con otro contenido.", 409);
            return MovementView.From(existing);
        }
        var c = await FindAccount(input.CuentaId, ct);
        await ActiveClient(c.ClienteId, ct);
        var fecha = input.Fecha?.UtcDateTime ?? DateTime.UtcNow;
        if (fecha > DateTime.UtcNow) throw new BusinessException("Fecha del movimiento no puede ser futura.");
        var latest = await repository.Latest(c.Id, ct);
        if (latest is not null && fecha < latest.Fecha)
            throw new BusinessException("Fecha anterior al último movimiento de la cuenta.", 409);
        var m = c.Register(id, input.Valor, fecha);
        repository.Add(m);
        await repository.Save(ct);
        return MovementView.From(m);
    }
    public async Task<MovementView> Correct(Guid id, MovementUpdate input, CancellationToken ct)
    {
        var m = await FindMovement(id, ct);
        var c = await FindAccount(m.CuentaId, ct);
        await ActiveClient(c.ClienteId, ct);
        var oldValue = m.Valor;
        c.Correct(m, input.Valor);
        repository.Add(new MovementAudit { MovimientoId = id, ValorAnterior = oldValue, ValorNuevo = input.Valor });
        await repository.Save(ct);
        return MovementView.From(m);
    }
    public async Task<Statement> Report(Guid cliente, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (desde > hasta || hasta == DateOnly.MaxValue) throw new BusinessException("Rango de fechas inválido.");
        var client = await repository.Client(cliente, ct) ?? throw new BusinessException("Cliente no encontrado en la proyección.", 404);
        return await repository.Report(client, desde, hasta, ct);
    }
    private async Task<Cuenta> FindAccount(Guid id, CancellationToken ct) =>
        await repository.Account(id, ct) ?? throw new BusinessException("Cuenta no encontrada.", 404);
    private async Task<Movimiento> FindMovement(Guid id, CancellationToken ct) =>
        await repository.Movement(id, ct) ?? throw new BusinessException("Movimiento no encontrado.", 404);
    private async Task ActiveClient(Guid id, CancellationToken ct)
    {
        var c = await repository.Client(id, ct) ?? throw new BusinessException("Cliente no disponible; espere su sincronización.", 409);
        if (!c.Estado || c.Eliminado) throw new BusinessException("Cliente inactivo o eliminado.", 409);
    }
    private static void ValidatePage(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100) throw new BusinessException("Página >= 1 y tamaño entre 1 y 100.");
    }
}
