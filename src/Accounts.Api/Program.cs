using Accounts.Application;
using Accounts.Infrastructure;
using Bank.Http;
using Microsoft.EntityFrameworkCore;

namespace Accounts.Api;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddDbContext<AccountsDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
        builder.Services.AddScoped<IAccountRepository, AccountRepository>();
        builder.Services.AddScoped<AccountService>();
        if (builder.Configuration.GetValue("Messaging:Enabled", true)) builder.Services.AddHostedService<ClientConsumer>();
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
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "accounts" }));
        app.MapGet("/health/ready", async (AccountsDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
        if (builder.Configuration.GetValue("Database:Initialize", true))
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.MigrateAsync();
        }
        await app.RunAsync();
    }
}
