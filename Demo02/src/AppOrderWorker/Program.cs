using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using AppOrderWorker.Domain;

namespace AppOrderWorker;

public class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // Declara a fila (caso não exista)
            await channel.QueueDeclareAsync(
                queue: "orderQueue",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            Console.WriteLine(" [*] Waiting for messages.");

            // Cria o consumidor assíncrono
            var consumer = new AsyncEventingBasicConsumer(channel);

            // Evento disparado ao receber uma nova mensagem
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    // 1. Recupera o body (byte[]) e converte para string
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // 2. Desserializa o JSON para objeto Order
                    var order = JsonSerializer.Deserialize<Order>(message);

                    Console.WriteLine($" [x] Order: {order.OrderNumber} | " +
                                      $"{order.ItemName} | {order.Price:N2}");

                    // 3. Confirma manualmente a mensagem, informando ao RabbitMQ
                    // que foi processada com sucesso
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    // Em caso de falha, faz NACK para reencaminhar
                    // a mensagem para a fila
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            // Inicia o consumo com autoAck = false (para ACK manual)
            await channel.BasicConsumeAsync(
                queue: "orderQueue",
                autoAck: false,
                consumer: consumer
            );

            // Mantém a aplicação ativa
            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}