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

        // Declara Exchange tipo Headers
        await channel.ExchangeDeclareAsync("business_exchange", ExchangeType.Headers);

        // Exemplo de envio da mensagem com header setor=financeiro
        var properties = new BasicProperties();
        properties.Headers = new Dictionary<string, object> { { "setor", "financeiro" } };

        string mensagem = "Nova mensagem para setor financeiro";
        var body = Encoding.UTF8.GetBytes(mensagem);

        // Publica no Exchange headers
        await channel.BasicPublishAsync(
            exchange: "business_exchange",
            routingKey: "",  // Não usado em Headers
            mandatory: false,
            basicProperties: properties,
            body: body
        );

        Console.WriteLine($"Enviada mensagem com header setor=financeiro");
    }
}
