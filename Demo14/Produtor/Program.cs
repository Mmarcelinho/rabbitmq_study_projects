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

        // Ativa Publisher Confirms
        channel.ConfirmSelect();

        // Eventos de confirmação
        channel.BasicAcks += Channel_BasicAcks;
        channel.BasicNacks += Channel_BasicNacks;
        channel.BasicReturn += Channel_BasicReturn;

        // Declaração da fila
        await channel.QueueDeclareAsync(queue: "order",
                                        durable: false,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null);

        string message = $"{DateTime.UtcNow:o} -> Hello World!";
        var body = Encoding.UTF8.GetBytes(message);

        // Publica mensagem com mandatory=true
        await channel.BasicPublishAsync(exchange: "",
                                        routingKey: "order", // Alterar para testar erros
                                        mandatory: true,
                                        basicProperties: null,
                                        body: body);

        try
        {
            bool confirmado = await channel.WaitForConfirmsAsync(TimeSpan.FromSeconds(5));

            if (confirmado)
                Console.WriteLine($"[x] Mensagem enviada com sucesso: {message}");
            else
                Console.WriteLine("[!] Mensagem NÃO confirmada pelo RabbitMQ.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Exceção ao confirmar: {ex.Message}");
        }

        Console.WriteLine("Pressione [enter] para sair.");
        Console.ReadLine();
    }

    private static void Channel_BasicAcks(object sender, BasicAckEventArgs e)
    {
        Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Ack");
    }

    private static void Channel_BasicNacks(object sender, BasicNackEventArgs e)
    {
        Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Nack");
    }

    private static void Channel_BasicReturn(object sender, BasicReturnEventArgs e)
    {
        var message = Encoding.UTF8.GetString(e.Body.ToArray());
        Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Return -> Mensagem original -> {message}");
    }
}
