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

        var channel = await CreateChannel(connection);

        // Declara a fila "order"
        await channel.QueueDeclareAsync(
            queue: "order",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // Inicia um único worker chamado Worker A
        await BuildAndRunWorker(channel, $"Worker A");

        Console.ReadLine();
    }

    public static async Task<IChannel> CreateChannel(this IConnection connection)
        => await connection.CreateChannelAsync();

    public static async Task BuildAndRunWorker(IChannel channel, string workerName)
    {
        await channel.BasicQosAsync(
            prefetchCount: 1,
            prefetchSize: 0,
            global: false
        );
        var consumer = new AsyncEventingBasicConsumer(channel);

        // Evento disparado a cada mensagem recebida
        consumer.ReceivedAsync += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"{channel.ChannelNumber} - {workerName}: [x] Received {message}");

                // Simulando sucesso de processamento:
                // Confirma (ACK) a mensagem especificamente
                channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                // Caso desejássemos disparar um erro:
                // throw new Exception("Erro ao processar mensagem");

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{channel.ChannelNumber} - {workerName}: [x] Error: {ex.Message}");

                // Em caso de falha, faz NACK e reenvia para a fila (requeue=true)
                channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                return Task.CompletedTask;
            }
        };

        // Agora, com autoAck = false (manual)
        await channel.BasicConsumeAsync(
            queue: "order",
            autoAck: false,      // <- MODO MANUAL DE ACK
            consumer: consumer
        );

        // Para manter o console ativo
        Console.ReadLine();
    }
}
