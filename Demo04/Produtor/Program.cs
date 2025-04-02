using RabbitMQ.Client;
using System.Text;

namespace Produtor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };

        // Abre 1 conexão TCP
        using var connection = await factory.CreateConnectionAsync();

        var queueName = "order";

        // Cria 2 channels a partir dessa conexão
        var channel1 = await CreateChannel(connection);
        var channel2 = await CreateChannel(connection);

        // Associa cada channel a um "Produtor" diferente
        BuildPublishers(channel1, queueName, "Produtor A");
        BuildPublishers(channel2, queueName, "Produtor B");

        Console.ReadLine();
    }

    // Helper para criar channels de forma assíncrona
    public static async Task<IChannel> CreateChannel(this IConnection connection)
        => await connection.CreateChannelAsync();

    // Método que efetivamente publica mensagens em loop
    public static void BuildPublishers(IChannel channel, string queue, string publisherName)
    {
        Task.Run(async () =>
        {
            int count = 0;

            // Declara a fila "order" (se não existir)
            await channel.QueueDeclareAsync(
                queue: queue,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            while (true)
            {
                // Prepara a mensagem
                string message = $"OrderNumber: {count++} from {publisherName}";
                var body = Encoding.UTF8.GetBytes(message);

                // Publica no RabbitMQ
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: "order",
                    body: body
                );

                // Intervalo de 1 segundo para envio
                Thread.Sleep(1000);
            }
        });
    }
}
