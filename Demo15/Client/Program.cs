using Client.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        var replyQueue = $"{nameof(Order)}_return";
        var correlationId = Guid.NewGuid().ToString();

        await channel.QueueDeclareAsync(queue: replyQueue, durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync(queue: nameof(Order), durable: false, exclusive: false, autoDelete: false, arguments: null);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var replyChannel = Channel.CreateUnbounded<string>();

        consumer.ReceivedAsync += async (model, ea) => 
        {
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                // Enfileira a resposta
                replyChannel.Writer.TryWrite(message);
            }
            else
            {
                Console.WriteLine($"Mensagem descartada, correlationId inválido: esperado={correlationId}, recebido={ea.BasicProperties.CorrelationId}");
            }
        };

        await channel.BasicConsumeAsync(queue: replyQueue, autoAck: true, consumer: consumer);

        var props = new BasicProperties();
        props.CorrelationId = correlationId;
        props.ReplyTo = replyQueue;

        while (true)
        {
            Console.Write("Informe o valor do pedido: ");
            if (!decimal.TryParse(Console.ReadLine(), out var amount))
            {
                Console.WriteLine("Valor inválido.");
                continue;
            }

            var order = new Order(amount);
            var message = JsonSerializer.Serialize(order);
            var body = Encoding.UTF8.GetBytes(message);

            await channel.BasicPublishAsync(exchange: "", routingKey: nameof(Order), mandatory: false, basicProperties: props, body: body);

            Console.WriteLine($"Published: {message}\nAguardando resposta...\n");

            // Aguarda a resposta de forma assíncrona
            var reply = await replyChannel.Reader.ReadAsync();

            Console.WriteLine($"Received: {reply}\n");
            Console.WriteLine("Pressione qualquer tecla para novo pedido...");
            Console.ReadKey();
            Console.Clear();
        }
    }
}
