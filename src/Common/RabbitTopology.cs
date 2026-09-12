using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Bank.Messaging;

internal static class RabbitTopology
{
    public const string Queue = "bank.client-changed.v1";
    public static IConnection Connect(IConfiguration configuration) => new ConnectionFactory
    {
        HostName = configuration["Rabbit:Host"] ?? "localhost",
        UserName = configuration["Rabbit:User"] ?? "bank",
        Password = configuration["Rabbit:Password"] ?? "bank-local-only",
        AutomaticRecoveryEnabled = true,
        RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
    }.CreateConnection();
    public static void Declare(IModel channel)
    {
        channel.QueueDeclare(Queue + ".dead", true, false, false);
        channel.QueueDeclare(Queue, true, false, false, new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = "",
            ["x-dead-letter-routing-key"] = Queue + ".dead"
        });
    }
}
