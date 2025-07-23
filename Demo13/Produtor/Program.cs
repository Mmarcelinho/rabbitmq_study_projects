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

        // Criação da fila persistente (durável)
        await channel.QueueDeclareAsync(
            queue: "fila_duravel",
            durable: true,        // Fila persistente
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        string mensagem = "Minha mensagem persistente";
        var body = Encoding.UTF8.GetBytes(mensagem);

        // Propriedade de persistência da mensagem
        var props = channel.CreateBasicProperties();
        props.Persistent = true;

        // Publicação da mensagem persistente
        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: "fila_duravel",
            basicProperties: props,
            body: body
        );

        Console.WriteLine("Mensagem persistente publicada com sucesso.");
    }
}
