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
        var channel = await connection.CreateChannelAsync();

        // Nome da fila vem por parâmetro, ex. "order" ou "log" ou "finance_orders"
        var queueName = args.Length > 0 ? args[0] : "order";

        // Declara a fila (garante que exista)
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // Configura prefetchCount para 1 (opcional, melhora balanceamento)
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"[Queue: {queueName}] [x] Received {message}");

                // ACK manual da mensagem
                channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Queue: {queueName}] [x] Error: {ex.Message}");
                channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                return Task.CompletedTask;
            }
        };

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine($"Consumer on queue '{queueName}' is running...");
        Console.ReadLine();
    }
}