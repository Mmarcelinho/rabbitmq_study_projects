using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consumidor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };

        // Abre 1 conexão
        using var connection = await factory.CreateConnectionAsync();

        // Exemplo: cria 2 channels (for k)
        for (int k = 0; k < 2; k++)
        {
            var channel = await CreateChannel(connection);

            // Declara a fila "order"
            await channel.QueueDeclareAsync(
                queue: "order",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Em cada channel, criamos 7 consumidores (for j)
            for (int j = 0; j < 7; j++)
            {
                var workerName = $"Worker {j + 1}";
                await BuildAndRunWorker(channel, workerName);
            }
        }

        Console.ReadLine();
    }

    public static async Task<IChannel> CreateChannel(this IConnection connection)
        => await connection.CreateChannelAsync();

    public static async Task BuildAndRunWorker(IChannel channel, string workerName)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            // ChannelNumber identifica qual canal está atendendo
            Console.WriteLine($"{channel.ChannelNumber} - {workerName}: [x] Received {message}");
            return Task.CompletedTask;
        };

        // Consumindo da mesma fila "order"
        await channel.BasicConsumeAsync(
            queue: "order",
            autoAck: true,
            consumer: consumer
        );

        // Aqui, apenas para manter o processo ativo
        Console.ReadLine();
    }
}
