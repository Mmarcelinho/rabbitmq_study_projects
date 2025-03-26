using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using WebAppOrder.Domain;

namespace WebAppOrder.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderController : ControllerBase
{
    private ILogger<OrderController> _logger;

    public OrderController(ILogger<OrderController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [Route("insert")]
    public async Task<IActionResult> InsertOrder(Order order)
    {
        try
        {
            // 1. Conexão com o RabbitMQ
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 2. Declara a fila "orderQueue" (caso não exista, será criada)
            await channel.QueueDeclareAsync(
                queue: "orderQueue",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // 3. Converte o objeto 'Order' em JSON e depois em byte array
            string message = JsonSerializer.Serialize(order);
            var body = Encoding.UTF8.GetBytes(message);

            // 4. Publica a mensagem na fila "orderQueue"
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "orderQueue",
                body: body
            );

            // Retorna HTTP 202 Accepted indicando que o pedido foi aceito para processamento
            return Accepted(order);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error inserting order", ex);
            return StatusCode(500);
        }
    }
}