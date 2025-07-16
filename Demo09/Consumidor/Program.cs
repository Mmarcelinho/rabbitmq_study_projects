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

        await channel.ExchangeDeclareAsync("topic_logs", ExchangeType.Topic);

        // Cria fila anônima (gerada automaticamente)
        var queueDeclareResult = await channel.QueueDeclareAsync();
        string queueName = queueDeclareResult.QueueName;

        // Binding keys (escuta múltiplos padrões, exemplo: todas mensagens do RJ)
        string[] bindingKeys = new[] { "finance.rj.*" };

        foreach (var bindingKey in bindingKeys)
        {
            await channel.QueueBindAsync(queue: queueName, exchange: "topic_logs", routingKey: bindingKey);
        }

        Console.WriteLine("[*] Esperando mensagens (finance.rj.*)");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var receivedRoutingKey = ea.RoutingKey;

            Console.WriteLine($"[x] Recebido '{receivedRoutingKey}':'{message}'");
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
        Console.WriteLine(" Pressione [enter] para sair.");
        Console.ReadLine();
    }
}