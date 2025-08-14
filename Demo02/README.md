# RabbitMQ com ASP.NET Core e Worker

## Visão Geral

Neste cenário, vamos:

1. **Executar o RabbitMQ em um contêiner Docker**
2. **Criar uma API ASP.NET Core** que publica mensagens em uma fila RabbitMQ
3. **Criar um aplicativo console** (Worker) que consome essas mensagens e as processa

Para tornarmos o sistema mais robusto, abordaremos **conceitos de durabilidade** (para que as mensagens não sejam perdidas caso o broker reinicie) e **configuração de prefetch** (_fair dispatch_, para distribuir tarefas de forma mais equilibrada entre múltiplos consumidores).

---

## Subindo o RabbitMQ via Docker

Para rodar o RabbitMQ usando Docker, pode-se utilizar um comando semelhante ao abaixo (adapte se necessário):

```bash
docker run -d --hostname my-rabbit --name rabbitmq-container -p 5672:5672 -p 15672:15672 rabbitmq:4.0-management
```

- **5672**: Porta padrão para conexões do protocolo AMQP (utilizada pelas aplicações).
- **15672**: Porta para acesso ao **Management Plugin** (interface web do RabbitMQ).

Após executar o comando:

- Acesse `http://localhost:15672` no navegador.
- Faça login com `guest / guest` (credenciais padrão, caso não tenha alterado).

---

## Criando a API (Publicadora de Mensagens)

### Estrutura do Projeto

- Trata-se de uma Web API ASP.NET Core contendo um **Controller** (`OrderController`).
- Ela recebe objetos do tipo `Order` via **POST**, converte em JSON e envia à fila RabbitMQ.

### Exemplo de Código

```csharp
namespace WebAppOrder.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> _logger;

    public OrderController(ILogger<OrderController> logger)
    {
        _logger = logger;
    }

    [HttpPost("insert")]
    public async Task<IActionResult> InsertOrder(Order order)
    {
        try
        {
            // 1. Conexão com o RabbitMQ
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 2. Declara a fila com durabilidade ativada
            await channel.QueueDeclareAsync(
                queue: "orderQueue",
                durable: true,   // <-- Torna a fila durável
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // 3. Converte o objeto em JSON e depois em bytes
            var message = JsonSerializer.Serialize(order);
            var body = Encoding.UTF8.GetBytes(message);

            // 4. Configura as propriedades da mensagem para persistência
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true; // <-- Mensagens persistentes

            // 5. Publica na fila
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "orderQueue",
                mandatory: false,
                basicProperties: properties,
                body: body
            );

            // Retorna HTTP 202 indicando aceite do processamento assíncrono
            return Accepted(order);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error inserting order", ex);
            return StatusCode(500);
        }
    }
}
```

#### Pontos de Observação

- **Fila durável** (`durable: true`): assim, se o RabbitMQ for reiniciado, a definição da fila permanece.
- **Mensagens persistentes** (`properties.Persistent = true`): reduz a chance de perda de mensagens em caso de crash do RabbitMQ (não é uma garantia 100%, mas na maioria dos casos é suficiente).
- **`return Accepted`**: status `202` indicando que o pedido foi aceito; o processamento efetivo será feito pelo Worker.

---

## Criando o Worker (Consumidor de Mensagens)

### Estrutura do Projeto

- Um projeto **Console** que se conecta ao RabbitMQ, escuta a fila `orderQueue` e processa cada mensagem.
- Ao final do processamento, envia um _ack_ confirmando que a mensagem foi processada com sucesso.

### Exemplo de Código

```csharp
namespace AppOrderWorker;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // Declara a fila como durável
            await channel.QueueDeclareAsync(
                queue: "orderQueue",
                durable: true,  // <-- fila durável
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Fair dispatch (prefetch=1): só entrega nova mensagem
            // ao worker quando ele terminar a anterior
            await channel.BasicQosAsync(
                prefetchSize: 0, 
                prefetchCount: 1, 
                global: false
            );

            Console.WriteLine(" [*] Waiting for messages.");

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    var order = JsonSerializer.Deserialize<Order>(message);

                    Console.WriteLine($" [x] Received: #{order.OrderNumber}, " + 
                                      $"Item: {order.ItemName}, Price: {order.Price:N2}");

                    // Simula processamento (ex.: await Task.Delay(...))

                    // Manual ACK
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception)
                {
                    // Se algo falhar, devolve a mensagem para a fila
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            // Consumo com autoAck=false para ACK manual
            await channel.BasicConsumeAsync(
                queue: "orderQueue",
                autoAck: false,
                consumer: consumer
            );

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
```

#### Pontos de Observação

1. **Fila Durável**: mesmo se o broker reiniciar, a definição da fila permanece.
2. **autoAck = false**: A confirmação da mensagem (ACK) é manual, garantindo que a mensagem seja removida da fila apenas depois de processada.
3. **PrefetchCount = 1** (Fair Dispatch): Impede que o RabbitMQ envie várias mensagens de uma vez a um mesmo consumidor; assim, cada Worker só recebe a próxima mensagem depois de concluir (e dar ACK) na anterior.
4. **BasicAckAsync**: ACK final para liberar a mensagem, que então é removida da fila.
5. **BasicNackAsync**: Se houver falha, podemos devolver a mensagem para reprocessamento (ou descartar, dependendo da estratégia).

---

## Testando o Fluxo

1. **Inicie o contêiner** com o RabbitMQ (caso ainda não esteja em execução).
2. **Rode o Worker** (Aplicação Console). Ele ficará aguardando as mensagens.
3. **Execute a API** e faça uma requisição **POST** no endpoint configurado, por exemplo:

    ```plaintext
    POST http://localhost:5000/api/order/insert
    Content-Type: application/json
    
    {
        "orderNumber": 123,
        "itemName": "Mouse Gamer",
        "price": 199.90
    }
    ```

4. **Verifique no console** do Worker o recebimento e o processamento da mensagem.

Se você iniciar **múltiplos** Workers, poderá observar como o RabbitMQ distribui as mensagens entre eles, respeitando a configuração de _prefetch_ e _durabilidade_.

---

## Conclusão

Com poucos ajustes, é possível:

- **Tornar filas e mensagens duráveis**, garantindo maior resiliência contra reinícios ou falhas do RabbitMQ.
- **Configurar prefetch** para implementar o _fair dispatch_, evitando que um único Worker receba uma carga desproporcional de mensagens.
- Manter a **arquitetura desacoplada**, publicando na API e consumindo no Worker, com confirmação e reenvio em caso de falhas.

Use este exemplo como ponto de partida para configurações avançadas (_exchanges_ personalizadas, _bindings_, _dead-letter queues_, _publisher confirms_, etc.) e crie sistemas altamente escaláveis e tolerantes a falhas.