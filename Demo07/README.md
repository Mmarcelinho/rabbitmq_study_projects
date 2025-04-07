Até agora, exploramos cenários de comunicação em que enviamos mensagens a filas específicas (ou ao _default exchange_, que faz um _direct_ implícito). Entretanto, há casos em que **precisamos replicar** a mesma mensagem para várias filas ao mesmo tempo. Para isso, o RabbitMQ disponibiliza diferentes tipos de **Exchanges**, entre eles o **Fanout**.

### 2. O que é um Exchange Fanout?

- Quando publicamos mensagens em uma **Exchange** do tipo **Fanout**, o RabbitMQ **distribui** (replica) automaticamente cada mensagem para **todas** as filas que estiverem _bindadas_ a essa Exchange.
    
- Em outras palavras, independentemente de _routing key_, **todas** as filas associadas ao Fanout Exchange recebem **uma cópia** da mensagem.

### 3. Exemplos de Uso

1. **Logs**: Quando queremos enviar a mesma mensagem de log para múltiplos destinos (ex.: filas de _monitoring_, _storage_, _analytics_).
    
2. **Broadcast**: Em aplicações de _broadcast_ em tempo real, cada consumidor (fila) recebe o mesmo dado ao mesmo tempo.
    
3. **Integrações**: Cópia de uma mesma mensagem de "Pedido" para a fila de “Financeiro”, “Logística” e “Auditoria”, por exemplo.

### 4. Exemplo de Código

#### 4.1 Produtor (Criando Exchange Fanout e Bindando Filas)

```csharp
using RabbitMQ.Client;
using System.Text;
using System.Threading.Channels;

namespace Produtor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        var manualResetEvent = new ManualResetEvent(false);

        // Bloqueia a thread principal
        manualResetEvent.Reset();

        // Abre 1 conexão TCP
        using var connection = await factory.CreateConnectionAsync();

        // Exemplo: Vamos criar/ligar 3 filas: "order", "log", "finance_orders"
        var channel1 = await SetupChannel(connection);
        var channel2 = await SetupChannel(connection);

        // Cria dois publicadores (Produtor A e B)
        BuildPublishers(channel1, "Produtor A", manualResetEvent);
        BuildPublishers(channel2, "Produtor B", manualResetEvent);

        // Aguarda até chamarmos manualResetEvent.Set()
        manualResetEvent.WaitOne();
    }

    public static async Task<IChannel> SetupChannel(this IConnection connection)
    {
        var channel = await connection.CreateChannelAsync();

        // Declara 3 filas
        await channel.QueueDeclareAsync("order", durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync("log", durable: false, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueueDeclareAsync("finance_orders", durable: false, exclusive: false, autoDelete: false, arguments: null);

        // Declara Exchange do tipo Fanout
        await channel.ExchangeDeclareAsync("order", ExchangeType.Fanout);

        // Faz o 'bind' de cada fila no Exchange "order"
        // (string.Empty) como routingKey pois no Fanout ela é ignorada
        await channel.QueueBindAsync("order", "order", string.Empty);
        await channel.QueueBindAsync("log", "order", string.Empty);
        await channel.QueueBindAsync("finance_orders", "order", string.Empty);

        return channel;
    }

    public static void BuildPublishers(IChannel channel, string publisherName, ManualResetEvent manual)
    {
        Task.Run(async () =>
        {
            int count = 0;
            while (true)
            {
                try
                {
                    Console.WriteLine($"Press [Enter] to send 100 messages from {publisherName}...");
                    Console.ReadLine();

                    for (int i = 0; i < 100; i++)
                    {
                        string message = $"OrderNumber: {count++} from {publisherName}";
                        var body = Encoding.UTF8.GetBytes(message);

                        // Publica no Exchange "order" (tipo Fanout)
                        // routingKey é ignorado neste modo
                        await channel.BasicPublishAsync(
                            exchange: "order",
                            routingKey: string.Empty,
                            body: body
                        );

                        Console.WriteLine($"{publisherName} - [x] Sent: {message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR in {publisherName}]: {ex.Message}");
                    // Caso queiramos encerrar a aplicação se algo falhar
                    manual.Set();
                }
            }
        });
    }
}
```

**Notas**:

- `ExchangeDeclareAsync("order", ExchangeType.Fanout)` cria uma Exchange chamada “order” do tipo **Fanout**.
    
- **Bindings**: `QueueBindAsync("order", "order", string.Empty)` indica que a fila `"order"` recebe mensagens publicadas em `"order"` Exchange. Podemos repetir para quantas filas quisermos.
    
- Quando publicamos (`BasicPublishAsync`) no Exchange “order”, **todas** as filas bindadas recebem **cópias** da mensagem.

---

#### 4.2 Consumidor (Consumindo cada Fila)

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

        // Abre 1 conexão
        using var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        // Nome da fila vem por parâmetro, ex. "order" ou "log" ou "finance_orders"
        var queueName = args.Length > 0 ? args[0] : "order";

        // Declara a fila (garante que exista)
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // Configura prefetchCount para 1 (opcional, melhora balanceamento)
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"[Queue: {queueName}] [x] Received {message}");

                // ACK manual da mensagem
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

- Cada consumidor é ligado a uma **fila** específica. Como o Exchange **Fanout** envia mensagens para **todas as filas** associadas, cada consumidor (ligado a cada fila) receberá as mesmas mensagens.
    
- Podemos rodar este executável múltiplas vezes, cada vez passando um parâmetro diferente (`"order"`, `"log"`, `"finance_orders"`) para consumir de filas distintas.

---

### 5. Observações Importantes

1. **Uso do Fanout**
    
    - **routingKey** é **ignorada** nesse tipo de exchange. A mensagem vai para **todas** as filas bindadas.
        
    - Bom para _broadcast_, mas pode gerar grande volume de mensagens duplicadas.
    
2. **Outros Tipos de Exchange**
    
    - **Direct**: Roteia conforme `routingKey` exata.
        
    - **Topic**: Roteia por _pattern_ (curinga) de `routingKey`.
        
    - **Headers**: Usa cabeçalhos (Headers) para determinar roteamento.
    
3. **Produtor**
    
    - Agora publica em uma **Exchange** (`exchange: "order"`) em vez de publicar diretamente na fila.
    
4. **Consumidor**
    
    - Continua consumindo de uma **fila** normal. As filas recebem as mensagens da _Fanout Exchange_ via **binding**.
    
5. **Balanceamento**
    
    - É possível combinar com `BasicQos` (prefetch) para cada consumidor gerenciar melhor seu volume de mensagens, especialmente quando há múltiplos consumidores por fila.
    
6. **Escalabilidade**
    
    - Se preciso replicar a mesma informação para várias filas (e, consequentemente, para vários serviços), o Fanout Exchange simplifica a arquitetura.

---

### 6. Conclusão

O **Fanout Exchange** é perfeito para casos em que desejamos **copiar** uma mesma mensagem para diversos destinos (filas). Ele oferece uma forma simples de _broadcast_ sem precisar enviar manualmente a mensagem para cada fila. Cada nova fila que se _bindar_ ao Exchange passará a receber **todas** as mensagens publicadas nele.

Esse padrão facilita cenários como:

- **Logs centralizados**: toda mensagem é enviada a várias filas, cada qual tratada por equipes ou propósitos diferentes (monitoramento, auditoria, etc.).
    
- **Notificações** simultâneas em diferentes sistemas.
