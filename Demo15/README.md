# RPC (Remote Procedure Call) no RabbitMQ

## Introdução

O **RPC (Remote Procedure Call)** com RabbitMQ permite que um sistema envie uma solicitação para outro serviço, aguarde o processamento e receba a resposta pela própria infraestrutura de mensageria.  
Diferente de um simples _publish/subscribe_, o RPC implementa um fluxo **ida e volta**:

1. O **cliente** publica a mensagem (pedido) em uma fila de requisição.
2. O **servidor** consome essa mensagem, processa a lógica de negócio e devolve uma resposta.
3. O cliente recebe a resposta e continua seu processamento.

Esse padrão é muito útil quando precisamos:

- Realizar uma operação remota que deve retornar um resultado.
- Manter um baixo acoplamento entre cliente e servidor, mas ainda com comunicação síncrona (do ponto de vista do cliente).

---

## Conceitos-Chave no RabbitMQ para RPC

### Reply Queue

- É a **fila de resposta** definida pelo cliente no momento de enviar a mensagem.
- Usada para o servidor saber onde devolver o resultado.
- Pode ser **dinâmica** e **auto-delete** (gerada com um identificador único por requisição) ou fixa.

### CorrelationId

- Identificador único gerado pelo cliente ao publicar a mensagem.
- Usado para associar a resposta à requisição original.
- Ao receber uma mensagem na fila de resposta, o cliente verifica se o `CorrelationId` recebido é o mesmo da requisição.

---

## Fluxo de Comunicação RPC

1. Cliente cria a **fila de resposta** e define um `CorrelationId`.
2. Cliente publica a mensagem na fila de requisição, configurando:
   - `ReplyTo` com o nome da fila de resposta.
   - `CorrelationId` com o identificador único.
3. Servidor consome a mensagem:
   - Processa a lógica de negócio.
   - Publica a resposta na fila definida em `ReplyTo`, preservando o mesmo `CorrelationId`.
4. Cliente lê a fila de resposta:
   - Se `CorrelationId` corresponder, processa o resultado.
   - Caso contrário, descarta ou redireciona.

---

## Estrutura do Exemplo

No exemplo a seguir, temos:

- **Cliente (Client)**: envia pedidos e aguarda respostas.
- **Servidor (Server)**: processa pedidos e envia respostas.

O **domínio** trabalha com a entidade `Order`, que possui:

- Valor do pedido (`Amount`)
- Status (`Processing`, `Approved`, `Declined`)

O servidor aplica a regra de negócio:

- Valor < 0 ou > 10.000 → `Declined`
- Caso contrário → `Approved`

---

## Código do Cliente (Publicador + Consumidor de Resposta)

```csharp
namespace Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        var replyQueue = $"{nameof(Order)}_return";
        var correlationId = Guid.NewGuid().ToString();

        await channel.QueueDeclareAsync(replyQueue, false, false, false, null);
        await channel.QueueDeclareAsync(nameof(Order), false, false, false, null);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var replyChannel = Channel.CreateUnbounded<string>();

        consumer.ReceivedAsync += async (model, ea) =>
        {
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                replyChannel.Writer.TryWrite(message);
            }
            else
            {
                Console.WriteLine($"Mensagem descartada. Esperado: {correlationId}, Recebido: {ea.BasicProperties.CorrelationId}");
            }
        };

        await channel.BasicConsumeAsync(replyQueue, true, consumer);

        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = replyQueue
        };

        while (true)
        {
            Console.Write("Informe o valor do pedido: ");
            if (!decimal.TryParse(Console.ReadLine(), out var amount))
            {
                Console.WriteLine("Valor inválido.");
                continue;
            }

            var order = new Order(amount);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order));

            await channel.BasicPublishAsync("", nameof(Order), false, props, body);

            Console.WriteLine($"Publicado: {amount}\nAguardando resposta...\n");

            var reply = await replyChannel.Reader.ReadAsync();
            Console.WriteLine($"Resposta: {reply}\n");

            Console.WriteLine("Pressione qualquer tecla para novo pedido...");
            Console.ReadKey();
            Console.Clear();
        }
    }
}
```

---

## Código do Servidor (Consumidor + Publicador de Resposta)

```csharp
namespace Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var consumer = await InitializeConsumer(channel, nameof(Order));

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var incomingMessage = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($"{DateTime.Now:o} Recebido => {incomingMessage}");

                var order = JsonSerializer.Deserialize<Order>(incomingMessage);
                order.SetStatus(ProcessOrderStatus(order.Amount));

                var replyMessage = JsonSerializer.Serialize(order);
                Console.WriteLine($"{DateTime.Now:o} Respondendo => {replyMessage}");

                SendReply(replyMessage, channel, ea);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex}");
            }
        };

        Console.WriteLine(" [x] Aguardando requisições RPC. Pressione ENTER para sair.");
        Console.ReadLine();
    }

    private static OrderStatus ProcessOrderStatus(decimal amount)
        => OrderService.OnStore(amount);

    private static void SendReply(string replyMessage, IChannel channel, BasicDeliverEventArgs ea)
    {
        var replyProps = new BasicProperties
        {
            CorrelationId = ea.BasicProperties.CorrelationId
        };

        var responseBytes = Encoding.UTF8.GetBytes(replyMessage);

        channel.BasicPublishAsync("", ea.BasicProperties.ReplyTo, false, replyProps, responseBytes);
        channel.BasicAckAsync(ea.DeliveryTag, false);
    }

    private static async Task<AsyncEventingBasicConsumer> InitializeConsumer(IChannel channel, string queueName)
    {
        await channel.QueueDeclareAsync(queueName, false, false, false, null);
        await channel.BasicQosAsync(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        await channel.BasicConsumeAsync(queueName, false, consumer);

        return consumer;
    }
}
```

---

## Pontos Importantes

### Uso do Channel.CreateUnbounded<string>() no Cliente

Permite aguardar a resposta assincronamente sem bloquear a aplicação.

### BasicQos(0, 1, false) no Servidor

Garante que apenas uma mensagem seja processada por vez.

### Validação do CorrelationId

Evita que o cliente processe respostas que não foram originadas pela sua requisição.

### Definição de ReplyTo

É o mecanismo que diz ao servidor para onde enviar a resposta.
