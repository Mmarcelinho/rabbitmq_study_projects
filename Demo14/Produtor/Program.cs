using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Produtor;

public static class Program
{
    private static TaskCompletionSource<bool>? _confirmTcs;

    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };

        var channelOpts = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(channelOpts);

        // Eventos de confirmação e retorno de mensagem
        channel.BasicAcksAsync += OnBasicAcksAsync;
        channel.BasicNacksAsync += OnBasicNacksAsync;
        channel.BasicReturnAsync += OnBasicReturnAsync;

        await channel.QueueDeclareAsync(
            queue: "order",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        string message = $"{DateTime.UtcNow:o} -> Hello World!";
        var body = Encoding.UTF8.GetBytes(message);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Controle de confirmação do Publisher Confirms
        _confirmTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var props = new BasicProperties();

        await channel.BasicPublishAsync<BasicProperties>(
            exchange: "",
            routingKey: "orderssssss", // rota incorreta para gerar BasicReturn
            mandatory: true,
            basicProperties: props,
            body: body,
            cancellationToken: cts.Token);

        try
        {
            bool confirmado = await _confirmTcs.Task.WaitAsync(cts.Token);

            if (confirmado)
                Console.WriteLine($"[x] Mensagem ACK pelo broker: {message}");
            else
                Console.WriteLine("[!] Mensagem NACK pelo broker.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[!] Timeout aguardando Publisher Confirm.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Erro ao publicar/confirmar: {ex.Message}");
        }

        Console.WriteLine("Pressione [enter] para sair.");
        Console.ReadLine();
    }

    private static Task OnBasicAcksAsync(object? sender, BasicAckEventArgs e)
    {
        _confirmTcs?.TrySetResult(true);
        Console.WriteLine($"{DateTime.UtcNow:o} -> BasicAck (deliveryTag: {e.DeliveryTag}, multiple: {e.Multiple})");
        return Task.CompletedTask;
    }

    private static Task OnBasicNacksAsync(object? sender, BasicNackEventArgs e)
    {
        _confirmTcs?.TrySetResult(false);
        Console.WriteLine($"{DateTime.UtcNow:o} -> BasicNack (deliveryTag: {e.DeliveryTag}, multiple: {e.Multiple}, requeue: {e.Requeue})");
        return Task.CompletedTask;
    }

    private static Task OnBasicReturnAsync(object? sender, BasicReturnEventArgs e)
    {
        var returnedBody = Encoding.UTF8.GetString(e.Body.ToArray());
        Console.WriteLine($"{DateTime.UtcNow:o} -> BasicReturn " +
                          $"[replyCode={e.ReplyCode}, replyText={e.ReplyText}, exchange='{e.Exchange}', routingKey='{e.RoutingKey}'] " +
                          $"Mensagem original: {returnedBody}");
        return Task.CompletedTask;
    }
}
