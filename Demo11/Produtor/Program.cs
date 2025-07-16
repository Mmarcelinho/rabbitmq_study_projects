using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Produtor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Exchange e Queue DLQ
        await channel.ExchangeDeclareAsync("DeadLetterExchange", ExchangeType.Fanout);
        await channel.QueueDeclareAsync("DeadLetterQueue", durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync("DeadLetterQueue", "DeadLetterExchange", routingKey: "");

        // Argumentos DLQ para a fila principal
        var arguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "DeadLetterExchange" }
        };

        await channel.QueueDeclareAsync("task_queue",
                                        durable: true,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: arguments);

        while (true)
        {
            Console.Write("Digite uma mensagem: ");
            var message = Console.ReadLine();

            var body = Encoding.UTF8.GetBytes(message);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;

            await channel.BasicPublishAsync(exchange: "",
                                            routingKey: "task_queue",
                                            basicProperties: properties,
                                            body: body);

            Console.WriteLine($"[x] Enviado '{message}'");
        }
    }
}
