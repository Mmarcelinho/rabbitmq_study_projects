using RabbitMQ.Client;
using System.Text;

namespace RProducer;

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
            queue: "qwebapp",
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
            string message = $"Hello World! - Count: {count++}";
            var body = Encoding.UTF8.GetBytes(message);

            // Publica a mensagem na fila "qwebapp"
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "qwebapp",
                body: body
            );

            Console.WriteLine($" [x] Sent {message}");

            // Pequeno delay para visualizar melhor o envio
            Thread.Sleep(200);
        }
    }
}