# Uso de **ACK Manual** (Manual Acknowledgment)

Neste material, exploramos como substituir o `autoAck` pela confirmação manual de mensagens (via `BasicAckAsync` e `BasicNackAsync`) no RabbitMQ para evitar perda de mensagens e aumentar a resiliência em caso de erros.

---

## Cenário Apresentado

Há dois projetos em C#:

1. **Produtor**: envia mensagens para a fila `order`, em lotes de 100 mensagens sempre que pressionamos uma tecla.
    
2. **Consumidor**: recebe as mensagens da fila. Em vez de usar `autoAck = true`, é possível ativar o modo manual (autoAck = false) para controlar quando a mensagem é de fato confirmada (ACK) ou descartada/reenviada (NACK).

A ideia é demonstrar por que o `autoAck = true` pode levar à perda de mensagens caso um _worker_ (consumidor) falhe antes de processar corretamente os dados.

---

## Principais Conceitos

1. **autoAck = true**: Assim que o broker entrega a mensagem ao consumidor, ela é marcada como concluída no servidor. Se ocorrer um erro ou crash antes da lógica de negócios terminar, a mensagem estará perdida.
    
2. **autoAck = false** (Manual ACK): O consumidor somente confirma a mensagem para o broker depois de processá-la com sucesso. Caso haja falha, ele emite um NACK (ou não envia ACK), e a mensagem **retorna** para a fila, podendo ser consumida novamente por outro _worker_ ou no retry.
    
3. **BasicAckAsync**: Confirma a mensagem (com `deliveryTag`) para o broker, removendo-a da fila definitivamente.
    
4. **BasicNackAsync**: Informa que não foi possível processar a mensagem. Podemos escolher devolvê-la para a fila (`requeue=true`) ou descartá-la (`requeue=false`).

---

## Códigos de Exemplo

### Consumidor (Manual ACK/NACK)

```csharp
namespace Consumidor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };

        // Abre 1 conexão
        using var connection = await factory.CreateConnectionAsync();

        var channel = await CreateChannel(connection);

        // Declara a fila "order"
        await channel.QueueDeclareAsync(
            queue: "order",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        // Inicia um único worker chamado Worker A
        await BuildAndRunWorker(channel, $"Worker A");

        Console.ReadLine();
    }

    public static async Task<IChannel> CreateChannel(this IConnection connection)
        => await connection.CreateChannelAsync();

    public static async Task BuildAndRunWorker(IChannel channel, string workerName)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);

        // Evento disparado a cada mensagem recebida
        consumer.ReceivedAsync += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"{channel.ChannelNumber} - {workerName}: [x] Received {message}");

                // Simulando sucesso de processamento:
                // Confirma (ACK) a mensagem especificamente
                channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                // Caso desejássemos disparar um erro:
                // throw new Exception("Erro ao processar mensagem");

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{channel.ChannelNumber} - {workerName}: [x] Error: {ex.Message}");

                // Em caso de falha, faz NACK e reenvia para a fila (requeue=true)
                channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                return Task.CompletedTask;
            }
        };

        // Agora, com autoAck = false (manual)
        await channel.BasicConsumeAsync(
            queue: "order",
            autoAck: false,      // <- MODO MANUAL DE ACK
            consumer: consumer
        );

        // Para manter o console ativo
        Console.ReadLine();
    }
}
```

**Notas Importantes**

- `autoAck: false` faz com que o RabbitMQ aguarde explicitamente pelo ACK ou NACK do consumidor.
    
- Em caso de exceção, usamos `BasicNackAsync(deliveryTag, false, true)` para reencaminhar a mensagem à fila.
    
- Se a aplicação encerrar antes do ACK, o RabbitMQ também reencaminha as mensagens não confirmadas de volta à fila.

---

### Produtor (Publicando Mensagens em Lote)

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

        // ManualResetEvent bloqueia a thread principal até liberarmos
        manualResetEvent.Reset();
        
        // Abre 1 conexão TCP
        using var connection = await factory.CreateConnectionAsync();

        var queueName = "order";

        // Cria 2 channels a partir dessa conexão (ex.: Produtor A e B)
        var channel1 = await CreateChannel(connection, queueName);
        var channel2 = await CreateChannel(connection, queueName);

        BuildPublishers(channel1, queueName, "Produtor A", manualResetEvent);
        BuildPublishers(channel2, queueName, "Produtor B", manualResetEvent);

        // Aguarda até chamarmos manualResetEvent.Set()
        manualResetEvent.WaitOne();
    }

    // Helper para criar channels de forma assíncrona
    public static async Task<IChannel> CreateChannel(this IConnection connection, string queueName)
    {
        var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        return channel;
    }

    // Publica mensagens em loop
    public static void BuildPublishers(IChannel channel, string queue, string publisherName, ManualResetEvent manual)
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

                        // Publica no RabbitMQ
                        await channel.BasicPublishAsync(
                            exchange: string.Empty,
                            routingKey: "order",
                            body: body
                        );

                        Console.WriteLine($"{publisherName} - [x] Sent: {message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR in {publisherName}]: {ex.Message}");
                    // Caso queiramos encerrar a aplicação se algo falhar:
                    manual.Set();
                }
            }
        });
    }
}
```

**Notas**

- A cada `Enter`, são enviados 100 mensagens.
    
- O `ManualResetEvent` é usado para manter a aplicação aberta.
    
- Caso ocorra uma exceção no loop de publicação, podemos disparar `manual.Set()` e encerrar a execução.

---

## Motivações para Manual ACK

1. **Prevenção de perda de mensagens**: Se o consumidor falhar antes de terminar o processamento, o RabbitMQ notará a ausência de ACK e **recolocará** a mensagem na fila.
    
2. **Observabilidade e Resiliência**: Permite controlar erros de negócio, reenviar mensagens para a fila (`NACK`) ou redirecionar para outra fila de DLQ (Dead Letter Queue).
    
3. **Workflow**: Em cenários complexos, podemos só dar **ACK** após o processamento completo (por exemplo, salvando em banco, chamando serviços externos etc.).

---

> _“... se você usar `autoAck = true`, assim que a mensagem é lida, o RabbitMQ a marca como concluída. Se o worker cair no meio do processo, você perde a mensagem.  
> Com `autoAck = false`, você precisa dar `channel.BasicAckAsync()` manualmente depois de processar. Se der erro, pode chamar `BasicNackAsync()` e reencaminhar a mensagem à fila. A fila aguarda o ACK. Se o worker fechar antes de confirmar, RabbitMQ entende que não houve ACK e a mensagem volta para a fila.”_

---

## Conclusões

1. **autoAck = false** (Manual Acknowledgment) traz maior **segurança** para suas filas.
    
2. **ACK** ou **NACK** garante que nenhuma mensagem será “perdida” caso um consumidor encerre ou gere erro.
    
3. **Produtores** podem continuar enviando mensagens normalmente, enquanto vários consumidores usam ACK manual para escalonar o processamento sem risco de perda.
    
4. Em produção, também se avalia a **persistência das mensagens** (durable e persistent) e possivelmente filas de **Dead Letter** para tratar mensagens que falham repetidamente.

