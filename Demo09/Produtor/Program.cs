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
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Cria exchange tipo Topic
        await channel.ExchangeDeclareAsync("topic_logs", ExchangeType.Topic);

        // Exemplo prático de envio de mensagem:
        var routingKey = "finance.rj.rj1";
        var message = "Mensagem específica para RJ1";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            exchange: "topic_logs",
            routingKey: routingKey,
            body: body);

        Console.WriteLine($"[x] Enviada '{routingKey}':'{message}'");
    }
}
