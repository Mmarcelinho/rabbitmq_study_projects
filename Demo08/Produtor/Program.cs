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
        var manualResetEvent = new ManualResetEvent(false);

        // Mantém a thread principal bloqueada
        manualResetEvent.Reset();

        using var connection = await factory.CreateConnectionAsync();

        // Setup do canal e do Exchange
        var channel1 = await SetupChannel(connection);
        var channel2 = await SetupChannel(connection);

        // Cria dois publishers (Produtor A e B)
        BuildPublishers(channel1, "Produtor A", manualResetEvent);
        BuildPublishers(channel2, "Produtor B", manualResetEvent);

        manualResetEvent.WaitOne();
    }

    public static async Task<IChannel> SetupChannel(this IConnection connection)
    {
        var channel = await connection.CreateChannelAsync();

        // Declara filas
        await channel.QueueDeclareAsync("order", durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync("finance_orders", durable: false, exclusive: false, autoDelete: false, arguments: null);

        // Declara Exchange do tipo Direct
        await channel.ExchangeDeclareAsync("order", ExchangeType.Direct);

        // Binds: relaciona a fila com uma routing key específica
        await channel.QueueBindAsync("order", "order", "order_new");
        await channel.QueueBindAsync("order", "order", "order_upd");

        // "finance_orders" só recebe mensagens com "order_new"
        await channel.QueueBindAsync("finance_orders", "order", "order_new");

        return channel;
    }

    public static void BuildPublishers(IChannel channel, string publisherName, ManualResetEvent manual)
    {
        Task.Run(async () =>
        {
            int idIndex = 1;
            var random = new Random(DateTime.UtcNow.Millisecond * DateTime.UtcNow.Second);

            while (true)
            {
                try
                {
                    Console.WriteLine($"Press [Enter] to send messages from {publisherName}...");
                    Console.ReadLine();

                    // Cria um pedido "novo"
                    var order = new Order(idIndex++, random.Next(1000, 9999));
                    var message1 = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order));

                    // Publica a mensagem com routing key "order_new"
                    await channel.BasicPublishAsync(
                        exchange: "order",
                        routingKey: "order_new",
                        body: message1
                    );
                    Console.WriteLine($"New order Id {order.Id}: Amount {order.Amount} | Created: {order.CreatedDate:o}");

                    // Atualiza o pedido
                    order.UpdateOrder(random.Next(100, 999));
                    var message2 = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order));

                    // Publica com routing key "order_upd"
                    await channel.BasicPublishAsync(
                        exchange: "order",
                        routingKey: "order_upd",
                        body: message2
                    );
                    Console.WriteLine($"Updated order Id {order.Id}: Amount {order.Amount} | LastUpdated: {order.LastUpdated:o}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR in {publisherName}]: {ex.Message}");
                    manual.Set();
                }
            }
        });
    }
}
