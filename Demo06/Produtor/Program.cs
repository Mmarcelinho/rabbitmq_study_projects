using RabbitMQ.Client;
using System.Text;
using System.Threading.Channels;

namespace Produtor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        var manualResetEvent = new ManualResetEvent(false);

        // ManualResetEvent bloqueia a thread principal até liberarmos
        manualResetEvent.Reset();

        // Abre 1 conexão TCP
        using var connection = await factory.CreateConnectionAsync();

        var queueName = "order";

        // Cria 2 channels a partir dessa conexão (ex.: Produtor A e B)
        var channel1 = await CreateChannel(connection, queueName);
        var channel2 = await CreateChannel(connection, queueName);

        BuildPublishers(channel1, queueName, "Produtor A", manualResetEvent);
        BuildPublishers(channel2, queueName, "Produtor B", manualResetEvent);

        // Aguarda até chamarmos manualResetEvent.Set()
        manualResetEvent.WaitOne();
    }

    // Helper para criar channels de forma assíncrona
    public static async Task<IChannel> CreateChannel(this IConnection connection, string queueName)
    {
        var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        return channel;
    }

    // Publica mensagens em loop
    public static void BuildPublishers(IChannel channel, string queue, string publisherName, ManualResetEvent manual)
    {
        Task.Run(async () =>
        {
            int count = 0;

            while (true)
            {
                try
                {
                    Console.WriteLine($"Press [Enter] to send 100 messages from {publisherName}...");
                    Console.ReadLine();

                    for (int i = 0; i < 100; i++)
                    {
                        string message = $"OrderNumber: {count++} from {publisherName}";
                        var body = Encoding.UTF8.GetBytes(message);

                        // Publica no RabbitMQ
                        await channel.BasicPublishAsync(
                            exchange: string.Empty,
                            routingKey: "order",
                            body: body
                        );

                        Console.WriteLine($"{publisherName} - [x] Sent: {message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR in {publisherName}]: {ex.Message}");
                    // Caso queiramos encerrar a aplicação se algo falhar:
                    manual.Set();
                }
            }
        });
    }
}
