using System.Net;
using System.Net.Http.Json;
using Accounts.Application;
using Accounts.Domain;
using Accounts.Infrastructure;
using Clients.Application;
using Clients.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bank.IntegrationTests;

public sealed class ApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Client_post_saves_hash_and_outbox_atomically()
    {
        using var http = fixture.Clients.CreateClient();
        var input = ClientData();
        var response = await http.PostAsJsonAsync("/clientes", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw);
        Assert.DoesNotContain("contrasena", raw);
        var client = (await response.Content.ReadFromJsonAsync<ClientView>())!;
        using var scope = fixture.Clients.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClientsDbContext>();
        var stored = await db.Clientes.SingleAsync(c => c.Id == client.ClienteId);
        Assert.NotEqual(input.Contrasena, stored.PasswordHash);
        Assert.Contains(await db.Outbox.ToListAsync(), o => o.Payload.Contains(client.ClienteId.ToString()));
        var duplicate = await http.PostAsJsonAsync("/clientes", input);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Client_crud_persists_changes_and_deletion()
    {
        using var http = fixture.Clients.CreateClient();
        var input = ClientData();
        var created = await http.PostAsJsonAsync("/clientes", input);
        var c = (await created.Content.ReadFromJsonAsync<ClientView>())!;
        var url = $"/clientes/{c.ClienteId}";
        Assert.Equal(HttpStatusCode.OK, (await http.PutAsJsonAsync(url, input with { Nombre = "Actualizado" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PatchAsJsonAsync(url, new { telefono = "0980000000" })).StatusCode);
        var saved = (await http.GetFromJsonAsync<ClientView>(url))!;
        Assert.Equal("Actualizado", saved.Nombre);
        Assert.Equal("0980000000", saved.Telefono);
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Withdrawal_and_idempotency_preserve_balance()
    {
        using var http = fixture.Accounts.CreateClient();
        var account = await NewAccount(http, 100);
        var key = Guid.NewGuid();
        async Task<HttpResponseMessage> Withdraw(decimal value)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/movimientos")
            { Content = JsonContent.Create(new MovementInput(account.CuentaId, value)) };
            request.Headers.Add("Idempotency-Key", key.ToString());
            return await http.SendAsync(request);
        }
        Assert.Equal(HttpStatusCode.Created, (await Withdraw(-80)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Withdraw(-80)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Withdraw(-70)).StatusCode);
        var insufficient = await http.PostAsJsonAsync("/movimientos", new MovementInput(account.CuentaId, -30));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, insufficient.StatusCode);
        Assert.Contains("Saldo no disponible", await insufficient.Content.ReadAsStringAsync());
        var saved = (await http.GetFromJsonAsync<AccountView>($"/cuentas/{account.CuentaId}"))!;
        Assert.Equal(20, saved.SaldoDisponible);
        var movements = (await http.GetFromJsonAsync<List<MovementView>>($"/movimientos?cuenta={account.CuentaId}"))!;
        Assert.Single(movements);
    }

    [Fact]
    public async Task Report_is_inclusive_and_has_historical_closing_balance()
    {
        using var http = fixture.Accounts.CreateClient();
        var c = await NewAccount(http, 100);
        var day = new DateTimeOffset(2022, 2, 10, 23, 59, 59, TimeSpan.Zero);
        (await http.PostAsJsonAsync("/movimientos", new MovementInput(c.CuentaId, 600, day))).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/movimientos", new MovementInput(c.CuentaId, -200, day.AddDays(1)))).EnsureSuccessStatusCode();
        var statement = (await http.GetFromJsonAsync<Statement>($"/reportes?cliente={c.ClienteId}&fecha=2022-02-10,2022-02-10"))!;
        var account = Assert.Single(statement.Cuentas);
        Assert.Equal(700, account.SaldoDisponible);
        Assert.Equal(100, account.SaldoInicial);
        Assert.Equal(600, Assert.Single(account.Movimientos).Movimiento);
    }

    [Fact]
    public async Task Report_includes_accounts_without_movements()
    {
        using var http = fixture.Accounts.CreateClient();
        var c = await NewAccount(http, 35);
        var report = (await http.GetFromJsonAsync<Statement>($"/reportes?cliente={c.ClienteId}&desde=2022-02-01&hasta=2022-02-28"))!;
        var account = Assert.Single(report.Cuentas);
        Assert.Empty(account.Movimientos);
        Assert.Equal(35, account.SaldoDisponible);
    }

    [Fact]
    public async Task Corrections_are_audited_and_earlier_entries_are_protected()
    {
        using var http = fixture.Accounts.CreateClient();
        var c = await NewAccount(http, 100);
        var firstResponse = await http.PostAsJsonAsync("/movimientos", new MovementInput(c.CuentaId, -20));
        var first = (await firstResponse.Content.ReadFromJsonAsync<MovementView>())!;
        var update = await http.PutAsJsonAsync($"/movimientos/{first.MovimientoId}", new MovementUpdate(-10));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(90, (await update.Content.ReadFromJsonAsync<MovementView>())!.Saldo);
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        var audit = await db.Audits.SingleAsync(a => a.MovimientoId == first.MovimientoId);
        Assert.Equal(-20, audit.ValorAnterior);
        (await http.PostAsJsonAsync("/movimientos", new MovementInput(c.CuentaId, 10))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await http.PatchAsJsonAsync($"/movimientos/{first.MovimientoId}", new MovementUpdate(-5))).StatusCode);
    }

    [Fact]
    public async Task Concurrent_withdrawals_cannot_overdraw()
    {
        using var http = fixture.Accounts.CreateClient();
        var c = await NewAccount(http, 100);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            http.PostAsJsonAsync("/movimientos", new MovementInput(c.CuentaId, -80))));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r =>
            Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity }));
        Assert.Equal(20, (await http.GetFromJsonAsync<AccountView>($"/cuentas/{c.CuentaId}"))!.SaldoDisponible);
    }

    [Fact]
    public async Task Optimistic_concurrency_rejects_stale_write()
    {
        using var http = fixture.Accounts.CreateClient();
        var c = await NewAccount(http, 100);
        using var scope1 = fixture.Accounts.Services.CreateScope();
        using var scope2 = fixture.Accounts.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<AccountsDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AccountsDbContext>();
        var a = await db1.Cuentas.SingleAsync(x => x.Id == c.CuentaId);
        var b = await db2.Cuentas.SingleAsync(x => x.Id == c.CuentaId);
        db1.Movimientos.Add(a.Register(Guid.NewGuid(), -80, DateTime.UtcNow));
        b.Update("Corriente", true);
        await db1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Invalid_payload_and_inactive_account_are_rejected()
    {
        using var http = fixture.Accounts.CreateClient();
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/cuentas",
            new AccountInput(Guid.NewGuid(), "123", "Ahorros", 0))).StatusCode);
        var c = await NewAccount(http, 100);
        (await http.PatchAsJsonAsync($"/cuentas/{c.CuentaId}", new { estado = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/movimientos", new MovementInput(c.CuentaId, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync($"/reportes?cliente={c.ClienteId}&fecha=incorrecta")).StatusCode);
    }

    private static ClientInput ClientData() => new("Jose Lema", "Masculino", 35,
        Guid.NewGuid().ToString("N")[..20], "Otavalo sn y principal", "098254785", "1234");
    private async Task<AccountView> NewAccount(HttpClient http, decimal saldo)
    {
        using var scope = fixture.Accounts.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        var clientId = Guid.NewGuid();
        db.Clients.Add(new ClientSnapshot { Id = clientId, Nombre = "Cliente de prueba", Estado = true, Version = 1 });
        await db.SaveChangesAsync();
        var response = await http.PostAsJsonAsync("/cuentas", new AccountInput(clientId,
            Guid.NewGuid().ToString("N")[..20], "Ahorros", saldo));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountView>())!;
    }
}
