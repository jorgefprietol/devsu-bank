using Bank.Http;
using Clients.Application;
using Clients.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clients.Api;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddDbContext<ClientsDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
        builder.Services.AddScoped<IClientRepository, ClientRepository>();
        builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
        builder.Services.AddScoped<ClientService>();
        if (builder.Configuration.GetValue("Messaging:Enabled", true)) builder.Services.AddHostedService<OutboxPublisher>();
        builder.Services.AddExceptionHandler<ApiErrors>();
        builder.Services.AddProblemDetails();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "clients" }));
        app.MapGet("/health/ready", async (ClientsDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
        if (builder.Configuration.GetValue("Database:Initialize", true))
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ClientsDbContext>().Database.MigrateAsync();
        }
        await app.RunAsync();
    }
}
