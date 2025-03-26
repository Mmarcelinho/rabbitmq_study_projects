### 1. O que é Round-Robin?

No contexto do RabbitMQ, o **Round-Robin** é um modo de distribuição de mensagens entre múltiplos consumidores. Por padrão, quando existe mais de um consumidor “inscrito” (consumindo da mesma fila), o broker distribui as mensagens em sequência para cada consumidor, de forma circular.

Imagine que você possui três consumidores: **A**, **B** e **C**. A primeira mensagem vai para o consumidor **A**, a segunda para **B**, a terceira para **C**, a quarta novamente para **A**, e assim por diante, **ciclicamente**. Dessa forma, o RabbitMQ busca equilibrar a carga de mensagens entre todos os consumidores conectados.

### 2. Motivação (por que usar vários consumidores?)

Ter um **Produtor** gerando muitas mensagens pode levar a um acúmulo na fila se um único **Consumidor** não processar tudo rapidamente. Para lidar com alto volume, basta **adicionar mais consumidores**. Assim, se uma fila estiver acumulando mensagens, vários processos (Workers) podem consumir simultaneamente, ganhando desempenho e escalabilidade.

O round-robin é o comportamento **padrão** do RabbitMQ, portanto não é preciso nenhuma configuração especial para que a distribuição seja feita dessa maneira.

### 3. Exemplo Prático com Vários Consumidores

#### 3.1 Código do Produtor

Abaixo, um exemplo de produtor em .NET/C#, que envia mensagens para a fila `order` a cada 2 segundos:

```csharp
using RabbitMQ.Client;
using System.Text;

namespace Produtor;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configura a conexão com o RabbitMQ
        var factory = new ConnectionFactory { HostName = "localhost" };

        // Cria a conexão e o canal de comunicação
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Declara a fila (cria se ainda não existir)
        await channel.QueueDeclareAsync(
            queue: "order",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        int count = 0;

        // Loop infinito para enviar mensagens continuamente
        while (true)
        {
            // Monta a mensagem
            string message = $"OrderNumber: {count++}";
            var body = Encoding.UTF8.GetBytes(message);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "order",
                body: body
            );

            // A cada 2 segundos, envia uma nova mensagem
            Thread.Sleep(2000);
        }
    }
}
```

#### 3.2 Código do Consumidor

Aqui, um **Consumidor** simples que se conecta à mesma fila `order`. Repare que ele está em **loop** constante, aguardando mensagens.  
Para demonstrar o round-robin, **basta executar várias instâncias** desse mesmo Consumer em terminais/consoles diferentes:

```csharp
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consumidor;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configura a conexão com o RabbitMQ
        var factory = new ConnectionFactory { HostName = "localhost" };

        // Cria a conexão e o canal de comunicação
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Declara a fila (cria se ainda não existir)
        await channel.QueueDeclareAsync(
            queue: "order",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        Console.WriteLine(" [*] Waiting for messages.");

        // Cria um consumidor assíncrono
        var consumer = new AsyncEventingBasicConsumer(channel);

        // Evento disparado quando mensagens chegam na fila
        consumer.ReceivedAsync += (model, ea) =>
        {
            // Convertendo o corpo da mensagem para string
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            Console.WriteLine($" [x] Received {message}");
            return Task.CompletedTask;
        };

        // Inicia o consumo de mensagens
        // autoAck = true => mensagens removidas imediatamente após entrega
        await channel.BasicConsumeAsync(
            queue: "order",
            autoAck: true,
            consumer: consumer
        );

        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();
    }
}
```

### 4. Visualizando o Comportamento Round-Robin

1. **Suba o Produtor** (por exemplo, `dotnet run` no projeto _Produtor_). Ele enviará mensagens numeradas (`OrderNumber: 0`, `OrderNumber: 1`, etc.) a cada 2 segundos para a fila `order`.
    
2. **Abra duas ou mais janelas de console** e rode o mesmo executável de _Consumidor_ (ou `dotnet run` em múltiplas instâncias). Cada instância estará conectada na fila `order`.
    
3. Observe que as mensagens serão alternadas entre os consumidores:
    
    - Consumidor 1: OrderNumber: 0, OrderNumber: 2, OrderNumber: 4, ...
        
    - Consumidor 2: OrderNumber: 1, OrderNumber: 3, OrderNumber: 5, ...
        
    - E assim por diante, caso existam mais consumidores.
        

Se um dos consumidores for encerrado, o round-robin continuará automaticamente apenas entre os consumidores ativos.

### 5. Principais Benefícios

- **Escalonamento Horizontal**: conforme a fila cresce, você pode subir mais instâncias de consumidores para dar conta do volume.
    
- **Desacoplamento**: a lógica de produção das mensagens fica separada da lógica de consumo.
    
- **Equilíbrio Automático**: o próprio RabbitMQ se encarrega de intercalar a entrega das mensagens de forma circular entre todos os consumidores disponíveis.

### 6. Conclusão

O round-robin é o método **padrão** do RabbitMQ para distribuir mensagens quando há vários consumidores. Basta ter mais de um processo/instância consumindo a mesma fila que o broker se encarregará de distribuir as mensagens de modo circular, sem a necessidade de qualquer configuração adicional.

> **Dicas Finais**
> 
> - É possível rodar o mesmo código de consumidor em diferentes máquinas ou contêineres, aumentando a escalabilidade.
>     
> - Caso seja necessário **balancear** melhor a carga quando as tarefas têm tempos de processamento muito diferentes, pode-se configurar o **prefetch** (com `BasicQos`) para evitar que um consumidor receba muitas mensagens simultaneamente.
>     
> - Para maior confiabilidade, habilite **ACK manual** (autoAck=false) e durabilidade das mensagens (durable/persistent).
>    
