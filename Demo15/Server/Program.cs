using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Server.Domain;
using Server.Services;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var consumer = await InitializerConsumer(channel, nameof(Order));
     
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var incomingMessage = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($"{DateTime.Now:o} Incoming => {incomingMessage}");

                var order = JsonSerializer.Deserialize<Order>(incomingMessage);
                order.SetStatus(ProcessOrderStatus(order.Amount));

                var replyMessage = JsonSerializer.Serialize(order);
                Console.WriteLine($"{DateTime.Now:o} Reply => {replyMessage}");

                SendReplyMessage(replyMessage, channel, ea);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mensagem: {ex}");
            }
        };

        Console.WriteLine(" [x] Awaiting RPC requests. Pressione ENTER para sair.");
        Console.ReadLine();
    }

    private static OrderStatus ProcessOrderStatus(decimal amount)
    {
        return OrderService.OnStore(amount);
    }

    private static void SendReplyMessage(string replyMessage, IChannel channel, BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var replyProps = new BasicProperties();
        replyProps.CorrelationId = props.CorrelationId;

        var responseBytes = Encoding.UTF8.GetBytes(replyMessage);

        channel.BasicPublishAsync(exchange: "", routingKey: props.ReplyTo, mandatory: false,
            basicProperties: replyProps, body: responseBytes);

        channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
    }

    private static async Task<AsyncEventingBasicConsumer> InitializerConsumer(IChannel channel, string queueName)
    {
        await channel.QueueDeclareAsync(queue: queueName, durable: false,
            exclusive: false, autoDelete: false, arguments: null);

        await channel.BasicQosAsync(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

        return consumer;
    }
}