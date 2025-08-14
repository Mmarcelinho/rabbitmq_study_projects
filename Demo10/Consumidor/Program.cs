using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consumidor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Fila para consumir
        string queueName = "financeiro";

        await channel.QueueDeclareAsync(queue: queueName,
                                        durable: false,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null);

        // Binding com Header (setor=financeiro)
        await channel.QueueBindAsync(queue: queueName,
                                     exchange: "business_exchange",
                                     routingKey: "",  // Não usado em Headers
                                     arguments: new Dictionary<string, object> { { "setor", "financeiro" } });

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var mensagem = Encoding.UTF8.GetString(body);

            Console.WriteLine($"Recebido no setor financeiro: {mensagem}");

            await channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

        Console.WriteLine("Pressione [ENTER] para sair");
        Console.ReadLine();
    }
}
