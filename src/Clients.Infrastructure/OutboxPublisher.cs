using Bank.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clients.Infrastructure;

public sealed class OutboxPublisher(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<OutboxPublisher> logger) : BackgroundService
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
                channel.ConfirmSelect();
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ClientsDbContext>();
                    var pending = await db.Outbox.Where(m => m.PublishedAt == null)
                        .OrderBy(m => m.CreatedAt).Take(50).ToListAsync(stoppingToken);
                    foreach (var message in pending)
                    {
                        var props = channel.CreateBasicProperties();
                        props.Persistent = true;
                        props.ContentType = "application/json";
                        props.MessageId = message.Id.ToString();
                        channel.BasicPublish("", RabbitTopology.Queue, false, props,
                            System.Text.Encoding.UTF8.GetBytes(message.Payload));
                        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
                        message.PublishedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    await Task.Delay(500, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Publicación pendiente; se reintentará en 5 segundos.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
