using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Produtor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var queueName = "test_time_to_live";
        var factory = new ConnectionFactory() { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Declara a fila (sem TTL de fila, apenas para o exemplo)
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var body = Encoding.UTF8.GetBytes("Mensagem com TTL individual");
        var props = channel.CreateBasicProperties();
        props.Expiration = "10000"; // 10 segundos em milissegundos

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: queueName,
            basicProperties: props,
            body: body
        );

        Console.WriteLine("Mensagem publicada com TTL individual de 10 segundos.");
    }

    private void TtlForQueue()
    {
        {
            var queueName = "test_time_to_live";

            // Define TTL de 22 segundos para toda mensagem da fila
            var args = new Dictionary<string, object>
        {
            { "x-message-ttl", 22000 } // 22 segundos
        };

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: args
            );

            var body = Encoding.UTF8.GetBytes("Mensagem com TTL da fila (22 segundos)");

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body
            );

            Console.WriteLine("Mensagem publicada na fila com TTL padrão de 22 segundos.");
        }
    }
}