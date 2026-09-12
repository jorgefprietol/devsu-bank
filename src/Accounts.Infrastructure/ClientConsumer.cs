using System.Text.Json;
using Accounts.Domain;
using Bank.Contracts;
using Bank.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Accounts.Infrastructure;

public sealed class ClientConsumer(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<ClientConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = RabbitTopology.Connect(config);
                using var channel = connection.CreateModel();
                RabbitTopology.Declare(channel);
                while (!stoppingToken.IsCancellationRequested)
                {
                    var message = channel.BasicGet(RabbitTopology.Queue, false);
                    if (message is null) { await Task.Delay(200, stoppingToken); continue; }
                    ClientChanged e;
                    try
                    {
                        e = JsonSerializer.Deserialize<ClientChanged>(message.Body.Span)
                            ?? throw new JsonException("Evento vacío.");
                        if (e.ClienteId == Guid.Empty || e.Version <= 0 || string.IsNullOrWhiteSpace(e.Nombre) || e.Nombre.Length > 150)
                            throw new JsonException("Evento inválido.");
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex, "Evento inválido enviado a cola muerta. Id: {Id}", message.BasicProperties.MessageId);
                        channel.BasicReject(message.DeliveryTag, false);
                        continue;
                    }
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
                    var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == e.ClienteId, stoppingToken);
                    if (client is null)
                    {
                        client = new ClientSnapshot { Id = e.ClienteId };
                        db.Clients.Add(client);
                    }
                    // Re-delivery and out-of-order events are safe because versions only increase.
                    if (client.Version < e.Version)
                    {
                        client.Nombre = e.Nombre; client.Estado = e.Estado;
                        client.Eliminado = e.Eliminado; client.Version = e.Version;
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    channel.BasicAck(message.DeliveryTag, false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Closing the channel requeues any unacknowledged delivery.
                logger.LogWarning(ex, "Sincronización pendiente; se reintentará en 5 segundos.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
