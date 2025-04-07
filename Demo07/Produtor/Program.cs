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

        // Bloqueia a thread principal
        manualResetEvent.Reset();

        // Abre 1 conexão TCP
        using var connection = await factory.CreateConnectionAsync();

        // Exemplo: Vamos criar/ligar 3 filas: "order", "log", "finance_orders"
        var channel1 = await SetupChannel(connection);
        var channel2 = await SetupChannel(connection);

        // Cria dois publicadores (Produtor A e B)
        BuildPublishers(channel1, "Produtor A", manualResetEvent);
        BuildPublishers(channel2, "Produtor B", manualResetEvent);

        // Aguarda até chamarmos manualResetEvent.Set()
        manualResetEvent.WaitOne();
    }

    public static async Task<IChannel> SetupChannel(this IConnection connection)
    {
        var channel = await connection.CreateChannelAsync();

        // Declara 3 filas
        await channel.QueueDeclareAsync("order", durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync("log", durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync("finance_orders", durable: false, exclusive: false, autoDelete: false, arguments: null);

        // Declara Exchange do tipo Fanout
        await channel.ExchangeDeclareAsync("order", ExchangeType.Fanout);

        // Faz o 'bind' de cada fila no Exchange "order"
        // (string.Empty) como routingKey pois no Fanout ela é ignorada
        await channel.QueueBindAsync("order", "order", string.Empty);
        await channel.QueueBindAsync("log", "order", string.Empty);
        await channel.QueueBindAsync("finance_orders", "order", string.Empty);

        return channel;
    }

    public static void BuildPublishers(IChannel channel, string publisherName, ManualResetEvent manual)
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

                        // Publica no Exchange "order" (tipo Fanout)
                        // routingKey é ignorado neste modo
                        await channel.BasicPublishAsync(
                            exchange: "order",
                            routingKey: string.Empty,
                            body: body
                        );

                        Console.WriteLine($"{publisherName} - [x] Sent: {message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR in {publisherName}]: {ex.Message}");
                    // Caso queiramos encerrar a aplicação se algo falhar
                    manual.Set();
                }
            }
        });
    }
}