using RabbitMQ.Client;
using System.Text;

namespace Produtor;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configura a conexão com o RabbitMQ
        var factory = new ConnectionFactory { HostName = "localhost" };

        // Cria a conexão e o canal de comunicação
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Declara a fila (cria se ainda não existir)
        await channel.QueueDeclareAsync(
            queue: "order",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        int count = 0;

        // Loop infinito para enviar mensagens continuamente
        while (true)
        {
            // Monta a mensagem
            string message = $"OrderNumber: {count++}";
            var body = Encoding.UTF8.GetBytes(message);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "order",
                body: body
            );

            // A cada 2 segundos, envia uma nova mensagem
            Thread.Sleep(2000);
        }
    }
}