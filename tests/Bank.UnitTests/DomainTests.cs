using Accounts.Domain;
using Bank.Contracts;
using Clients.Domain;
using Clients.Infrastructure;
using Xunit;

namespace Bank.UnitTests;

public sealed class DomainTests
{
    [Fact]
    public void Cliente_inherits_persona_and_has_unique_identity()
    {
        var c = CreateClient();
        Assert.IsAssignableFrom<Persona>(c);
        Assert.NotEqual(Guid.Empty, c.Id);
        Assert.Equal("Jose Lema", c.Nombre);
        Assert.True(c.Estado);
        Assert.Equal(1, c.Version);
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(131)]
    public void Cliente_rejects_invalid_age(int age) => Assert.Throws<BusinessException>(() =>
        Cliente.Create("Jose", "Masculino", age, "123", "Quito", "099", "hash", true));
    [Fact]
    public void Delete_disables_client_without_losing_identity()
    {
        var c = CreateClient();
        var id = c.Id;
        c.Delete();
        Assert.Equal(id, c.Id);
        Assert.True(c.Eliminado);
        Assert.False(c.Estado);
        Assert.Equal(2, c.Version);
    }
    [Fact]
    public void Passwords_are_salted_and_never_stored_as_plaintext()
    {
        var hasher = new PasswordHasher();
        var first = hasher.Hash("1234");
        Assert.StartsWith("PBKDF2-SHA512:", first);
        Assert.NotEqual("1234", first);
        Assert.NotEqual(first, hasher.Hash("1234"));
    }
    [Theory]
    [InlineData(2000, -575, 1425)]
    [InlineData(100, 600, 700)]
    [InlineData(0, 150, 150)]
    [InlineData(540, -540, 0)]
    public void Movement_updates_balance(decimal initial, decimal value, decimal expected)
    {
        var account = Account(initial);
        var movement = account.Register(Guid.NewGuid(), value, DateTime.UtcNow);
        Assert.Equal(expected, account.SaldoDisponible);
        Assert.Equal(expected, movement.Saldo);
        Assert.Equal(value < 0 ? "Retiro" : "Deposito", movement.TipoMovimiento);
    }
    [Fact]
    public void Overdraft_is_rejected_without_changing_balance()
    {
        var account = Account(100);
        var ex = Assert.Throws<BusinessException>(() => account.Register(Guid.NewGuid(), -101, DateTime.UtcNow));
        Assert.Equal("Saldo no disponible", ex.Message);
        Assert.Equal(100, account.SaldoDisponible);
        Assert.Equal(0, account.UltimaSecuencia);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(0.001)]
    public void Invalid_amounts_are_rejected(decimal amount) => Assert.Throws<BusinessException>(() =>
        Account(100).Register(Guid.NewGuid(), amount, DateTime.UtcNow));
    [Fact]
    public void Earlier_movement_cannot_be_rewritten()
    {
        var account = Account(100);
        var first = account.Register(Guid.NewGuid(), 10, DateTime.UtcNow);
        account.Register(Guid.NewGuid(), 20, DateTime.UtcNow);
        Assert.Throws<BusinessException>(() => account.Correct(first, 50));
        Assert.Equal(130, account.SaldoDisponible);
    }
    [Fact]
    public void Last_movement_correction_recalculates_balance()
    {
        var account = Account(100);
        var m = account.Register(Guid.NewGuid(), -50, DateTime.UtcNow);
        account.Correct(m, -25);
        Assert.Equal(75, account.SaldoDisponible);
        Assert.Equal(75, m.Saldo);
    }
    private static Cliente CreateClient() => Cliente.Create("Jose Lema", "Masculino", 35, "1234567890", "Otavalo", "098254785", "hash", true);
    private static Cuenta Account(decimal initial) => Cuenta.Create(Guid.NewGuid(), "478758", "Ahorros", initial, true);
}
