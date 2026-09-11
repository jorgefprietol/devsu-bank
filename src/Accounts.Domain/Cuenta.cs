using Bank.Contracts;

namespace Accounts.Domain;

public sealed class Cuenta
{
    private Cuenta() { }
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public string NumeroCuenta { get; private set; } = "";
    public string TipoCuenta { get; private set; } = "";
    public decimal SaldoInicial { get; private set; }
    public decimal SaldoDisponible { get; private set; }
    public bool Estado { get; private set; }
    public long Version { get; private set; }
    public long UltimaSecuencia { get; private set; }

    public static Cuenta Create(Guid clienteId, string numero, string tipo, decimal saldo, bool estado)
    {
        if (clienteId == Guid.Empty) throw new BusinessException("ClienteId es obligatorio.");
        if (saldo < 0) throw new BusinessException("Saldo inicial no puede ser negativo.");
        var c = new Cuenta { Id = Guid.NewGuid(), ClienteId = clienteId,
            NumeroCuenta = Rules.Required(numero, "Número de cuenta", 30),
            SaldoInicial = Rules.Money(saldo), SaldoDisponible = saldo };
        c.Update(tipo, estado);
        return c;
    }
    public void Update(string tipo, bool estado)
    {
        TipoCuenta = tipo?.Trim().ToLowerInvariant() switch
        {
            "ahorros" => "Ahorros", "corriente" => "Corriente",
            _ => throw new BusinessException("Tipo de cuenta debe ser Ahorros o Corriente.")
        };
        Estado = estado;
        Version++;
    }
    public Movimiento Register(Guid id, decimal valor, DateTime fecha)
    {
        EnsureActive();
        if (valor == 0) throw new BusinessException("El movimiento no puede tener valor cero.");
        Rules.Money(valor);
        var saldo = Rules.Money(SaldoDisponible + valor);
        if (saldo < 0) throw new BusinessException("Saldo no disponible", 422);
        SaldoDisponible = saldo;
        Version++;
        UltimaSecuencia++;
        return new Movimiento(id, Id, fecha, valor, saldo, UltimaSecuencia);
    }
    // Only the latest entry may be corrected. Earlier corrections require a compensating entry.
    public void Correct(Movimiento movimiento, decimal valor)
    {
        EnsureActive();
        if (movimiento.CuentaId != Id || movimiento.Secuencia != UltimaSecuencia)
            throw new BusinessException("Solo puede corregirse el último movimiento; registre uno compensatorio.", 409);
        if (valor == 0) throw new BusinessException("El movimiento no puede tener valor cero.");
        Rules.Money(valor);
        var saldo = Rules.Money(SaldoDisponible - movimiento.Valor + valor);
        if (saldo < 0) throw new BusinessException("Saldo no disponible", 422);
        SaldoDisponible = saldo;
        Version++;
        movimiento.Correct(valor, saldo);
    }
    private void EnsureActive()
    {
        if (!Estado) throw new BusinessException("Cuenta inactiva.", 409);
    }
}

public sealed class Movimiento
{
    private Movimiento() { }
    internal Movimiento(Guid id, Guid cuentaId, DateTime fecha, decimal valor, decimal saldo, long secuencia)
    {
        Id = id; CuentaId = cuentaId; Fecha = fecha; Valor = valor; Saldo = saldo; Secuencia = secuencia;
        TipoMovimiento = valor > 0 ? "Deposito" : "Retiro";
    }
    public Guid Id { get; private set; }
    public Guid CuentaId { get; private set; }
    public DateTime Fecha { get; private set; }
    public string TipoMovimiento { get; private set; } = "";
    public decimal Valor { get; private set; }
    public decimal Saldo { get; private set; }
    public long Secuencia { get; private set; }
    internal void Correct(decimal valor, decimal saldo)
    {
        Valor = valor; Saldo = saldo; TipoMovimiento = valor > 0 ? "Deposito" : "Retiro";
    }
}

public sealed class ClientSnapshot
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Estado { get; set; }
    public bool Eliminado { get; set; }
    public long Version { get; set; }
}

public sealed class MovementAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MovimientoId { get; set; }
    public decimal ValorAnterior { get; set; }
    public decimal ValorNuevo { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
