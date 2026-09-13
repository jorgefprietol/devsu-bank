using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bank.IntegrationTests;

public sealed class ApiFactory<T>(string connection) : WebApplicationFactory<T> where T : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", connection);
        builder.UseSetting("Messaging:Enabled", "false");
    }
}

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public ApiFactory<Accounts.Api.Program> Accounts { get; private set; } = null!;
    public ApiFactory<Clients.Api.Program> Clients { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var conn = new NpgsqlConnection(postgres.GetConnectionString());
        await conn.OpenAsync();
        await using var command = new NpgsqlCommand("CREATE DATABASE clients_tests", conn);
        await command.ExecuteNonQueryAsync();
        var clientConnection = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString()) { Database = "clients_tests" };
        Accounts = new(postgres.GetConnectionString());
        Clients = new(clientConnection.ConnectionString);
    }
    public async Task DisposeAsync()
    {
        await Accounts.DisposeAsync();
        await Clients.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
