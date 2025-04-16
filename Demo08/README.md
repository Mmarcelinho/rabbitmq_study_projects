## **Exchange Direct** no RabbitMQ

### 1. Cenário

Em tutoriais anteriores, exploramos o **Fanout Exchange** (que envia cada mensagem para **todas** as filas _bindadas_). Agora, examinamos o **Direct Exchange**, que permite encaminhar mensagens para filas específicas com base em uma **routing key** exata.

Isso é útil quando queremos, por exemplo, ter filas diferentes para tipos distintos de mensagens, sem fazer broadcast para todas as filas.

---

### 2. Visão Geral do Exchange Direct

O **Direct Exchange** roteará a mensagem apenas para filas cujo _binding key_ corresponda exatamente à _routing key_ fornecida ao publicar.

Exemplo simplificado:

- **Fila A** está vinculada ao Exchange com _binding key_ `"order_new"`.
    
- **Fila B** está vinculada ao Exchange com _binding key_ `"order_upd"`.

Se publicarmos uma mensagem com **routing key** `"order_new"`, apenas a **Fila A** a receberá.

No caso do código abaixo, temos:

1. Uma fila _"order"_ (que recebe `"order_new"` e `"order_upd"`).
    
2. Uma fila _"finance_orders"_ (que recebe apenas `"order_new"`).

Dessa forma, as mensagens com routing key `"order_new"` são enviadas para **duas filas** (porque `"order"` e `"finance_orders"` ambas possuem _binding key_ `"order_new"`). Já as mensagens com routing key `"order_upd"` são enviadas **somente** para a fila `"order"`.

---

### 3. Código de Exemplo

#### 3.1 Classe de Domínio: `Order`

```csharp
namespace Produtor;

public class Order
{
    public long Id { get; private set; }
    public DateTime CreatedDate { get; }
    public DateTime LastUpdated { get; private set; }
    public long Amount { get; private set; }

    public Order(long id, long amount)
    {
        Id = id;
        CreatedDate = DateTime.UtcNow;
        LastUpdated = CreatedDate;
        Amount = amount;
    }

    public void UpdateOrder(long amount)
    {
        Amount = amount;
        LastUpdated = DateTime.UtcNow;
    }
}
```

**Observação**: Essa classe representa um pedido simples com `Id`, `CreatedDate`, `LastUpdated` e `Amount`.

---

#### 3.2 Produtor

```csharp
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
        var manualResetEvent = new ManualResetEvent(false);

        // Mantém a thread principal bloqueada
        manualResetEvent.Reset();

        using var connection = await factory.CreateConnectionAsync();

        // Setup do canal e do Exchange
        var channel1 = await SetupChannel(connection);
        var channel2 = await SetupChannel(connection);

        // Cria dois publishers (Produtor A e B)
        BuildPublishers(channel1, "Produtor A", manualResetEvent);
        BuildPublishers(channel2, "Produtor B", manualResetEvent);

        manualResetEvent.WaitOne();
    }

    public static async Task<IChannel> SetupChannel(this IConnection connection)
    {
        var channel = await connection.CreateChannelAsync();

        // Declara filas
        await channel.QueueDeclareAsync("order", durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync("finance_orders", durable: false, exclusive: false, autoDelete: false, arguments: null);

        // Declara Exchange do tipo Direct
        await channel.ExchangeDeclareAsync("order", ExchangeType.Direct);

        // Binds: relaciona a fila com uma routing key específica
        await channel.QueueBindAsync("order", "order", "order_new");
        await channel.QueueBindAsync("order", "order", "order_upd");

        // "finance_orders" só recebe mensagens com "order_new"
        await channel.QueueBindAsync("finance_orders", "order", "order_new");

        return channel;
    }

    public static void BuildPublishers(IChannel channel, string publisherName, ManualResetEvent manual)
    {
        Task.Run(async () =>
        {
            int idIndex = 1;
            var random = new Random(DateTime.UtcNow.Millisecond * DateTime.UtcNow.Second);

            while (true)
            {
                try
                {
                    Console.WriteLine($"Press [Enter] to send messages from {publisherName}...");
                    Console.ReadLine();

                    // Cria um pedido "novo"
                    var order = new Order(idIndex++, random.Next(1000, 9999));
                    var message1 = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order));

                    // Publica a mensagem com routing key "order_new"
                    await channel.BasicPublishAsync(
                        exchange: "order",
                        routingKey: "order_new",
                        body: message1
                    );
                    Console.WriteLine($"New order Id {order.Id}: Amount {order.Amount} | Created: {order.CreatedDate:o}");

                    // Atualiza o pedido
                    order.UpdateOrder(random.Next(100, 999));
                    var message2 = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order));

                    // Publica com routing key "order_upd"
                    await channel.BasicPublishAsync(
                        exchange: "order",
                        routingKey: "order_upd",
                        body: message2
                    );
                    Console.WriteLine($"Updated order Id {order.Id}: Amount {order.Amount} | LastUpdated: {order.LastUpdated:o}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR in {publisherName}]: {ex.Message}");
                    manual.Set();
                }
            }
        });
    }
}
```

##### Observações Principais

- **Exchange**: criada com `ExchangeType.Direct`.
    
- **Bindings**:
    
    - `"order"` é bindada com _routing key_ `"order_new"` e `"order_upd"`.
        
    - `"finance_orders"` é bindada apenas com _routing key_ `"order_new"`.
        
- **Publicações**:
    
    - Mensagens com routing key `"order_new"` chegam **a duas filas** (`"order"` e `"finance_orders"`).
        
    - Mensagens com routing key `"order_upd"` chegam **somente** à fila `"order"`.

---

#### 3.3 Consumidor

```csharp
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
        var channel = await connection.CreateChannelAsync();

        // Nome da fila por parâmetro (ex: "order" ou "finance_orders")
        var queueName = args.Length > 0 ? args[0] : "order";

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // BasicQos para melhorar balanceamento
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"[Queue: {queueName}] [x] Received {message}");

                // ACK manual
                channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Queue: {queueName}] [x] Error: {ex.Message}");
                channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                return Task.CompletedTask;
            }
        };

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine($"Consumer on queue '{queueName}' is running...");
        Console.ReadLine();
    }
}
```

**Notas**:

- **Consumidor** continua quase idêntico a outros exemplos anteriores.
    
- Usamos `autoAck = false` e `BasicAckAsync` (ou `BasicNackAsync`) para confirmar ou reenviar mensagens em caso de falha.
    
- Podemos rodar este consumidor múltiplas vezes, cada vez apontando para `"order"` ou `"finance_orders"` (ou ambos).

---

### 4. Funcionamento Prático

1. Ao gerar um **novo** pedido (routing key = `"order_new"`), a mensagem é **entregue** às filas `"order"` e `"finance_orders"`.
    
2. Ao **atualizar** um pedido (routing key = `"order_upd"`), a mensagem é **entregue** apenas à fila `"order"`.
    
3. Cada consumidor atrelado a essas filas recebe as mensagens correspondentes.

Esse **roteamento** é feito automaticamente pelo RabbitMQ com base nos _binds_ e _routing keys_ definidas.

---

### 5. Conclusão

O **Direct Exchange** é indicado quando precisamos despachar mensagens para filas específicas, dependendo de uma _routing key_ exata. Isso viabiliza cenários onde cada tipo de mensagem (por exemplo, `"new"`, `"upd"`, `"error"`, etc.) vai para filas diferentes, sem broadcast para todos.

**Dicas e Práticas**:

- Combine **Direct Exchanges** com outras estratégias (como filas de _Dead Letter_ para erros).
    
- Em cenários complexos, avalie se **Topic Exchange** não atenderia melhor (pois permite _wildcards_).
    
- Sempre monitore e teste cenários de publicação/consumo para verificar throughput e garantir roteamento conforme esperado.