using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consumidor;

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

        Console.WriteLine(" [*] Waiting for messages.");

        // Cria um consumidor assíncrono
        var consumer = new AsyncEventingBasicConsumer(channel);

        // Evento disparado quando mensagens chegam na fila
        consumer.ReceivedAsync += (model, ea) =>
        {
            // Convertendo o corpo da mensagem para string
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            Console.WriteLine($" [x] Received {message}");
            return Task.CompletedTask;
        };

        // Inicia o consumo de mensagens
        // autoAck = true => mensagens removidas imediatamente após entrega
        await channel.BasicConsumeAsync(
            queue: "order",
            autoAck: true,
            consumer: consumer
        );

        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();
    }
}