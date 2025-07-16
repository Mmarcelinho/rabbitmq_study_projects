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

        // Exchange e Queue DLQ
        await channel.ExchangeDeclareAsync("DeadLetterExchange", ExchangeType.Fanout);
        await channel.QueueDeclareAsync("DeadLetterQueue", durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync("DeadLetterQueue", "DeadLetterExchange", routingKey: "");

        var arguments = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "DeadLetterExchange" }
        };

        await channel.QueueDeclareAsync("task_queue",
                                        durable: true,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: arguments);

        await channel.BasicQosAsync(0, 1, false);

        Console.WriteLine(" [*] Esperando mensagens. Para sair pressione CTRL+C");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                int valor = int.Parse(message);
                Console.WriteLine($" [✔] Recebido e processado: {valor}");

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" [✖] Erro ao processar '{message}'. Enviando para DLQ.");

                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(queue: "task_queue",
                                        autoAck: false,
                                        consumer: consumer);

        Console.ReadLine();
    }
}
