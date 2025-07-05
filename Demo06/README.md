# Uso de **BasicQos** (Prefetch) e Balanceamento de Mensagens

## Contexto

Quando há múltiplos consumidores conectados a uma mesma fila, ou mesmo **um** consumidor que processa as mensagens manualmente (via _manual ACK_), precisamos regular quantas mensagens o RabbitMQ deve enviar por vez para cada canal/consumidor. Essa configuração se dá por meio do **`BasicQos`** e **prefetchCount**.

Por padrão (se não configurarmos o prefetch), o RabbitMQ pode entregar _muitas_ mensagens de uma só vez ao consumidor, especialmente se houver um grande volume de mensagens acumuladas. Isso pode resultar em um **desbalanceamento** indesejado: o primeiro consumidor a se conectar pode receber praticamente todas as mensagens pendentes, deixando os demais consumidores ociosos.

## Resumo do Problema

- **Sem `BasicQos`**: o primeiro consumidor que se conecta pode “puxar” quase todas as mensagens da fila, processando-as sozinho. Só depois que ele termina (ou enche seu buffer) o RabbitMQ passa a enviar mensagens aos outros consumidores.
    
- **Consequência**: se o consumidor cair durante o processamento sem ter confirmado (ACK) as mensagens, muitas mensagens voltam para a fila de uma só vez, atrasando o processamento. Além disso, o balanceamento de carga fica ineficiente, pois um consumidor pode ficar sobrecarregado e os outros subutilizados.

## `BasicQosAsync` e Parâmetros

No código abaixo, configuramos o `prefetchCount: 1` por canal para forçar o RabbitMQ a entregar **apenas uma mensagem por vez** antes de exigir um _ACK_ (confirmação). Isso garante um round-robin efetivo e evita que um consumidor acumule um grande lote de mensagens pendentes.

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
        // Configura prefetchCount = 1, ou seja, recebe só uma mensagem de cada vez
        await channel.BasicQosAsync(
            prefetchCount: 1,
            prefetchSize: 0,
            global: false
        );

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"{channel.ChannelNumber} - {workerName}: [x] Received {message}");

                // Se tudo der certo, ACK (confirma) a mensagem
                channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                // Caso quiséssemos simular erro:
                // throw new Exception("Erro ao processar mensagem");

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{channel.ChannelNumber} - {workerName}: [x] Error: {ex.Message}");
                // Em caso de falha, NACK e reencaminha para a fila
                channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                return Task.CompletedTask;
            }
        };

        // Consumo com autoAck = false, para uso de ACK manual
        await channel.BasicConsumeAsync(
            queue: "order",
            autoAck: false,
            consumer: consumer
        );

        Console.ReadLine();
    }
}
```

## Parâmetros do `BasicQosAsync`

- **`prefetchSize`**: (normalmente 0) – tamanho máximo em bytes que o consumidor pode receber sem confirmar. O RabbitMQ .NET Client atual ignora esse campo, então sempre deixe 0.
    
- **`prefetchCount`**: número de mensagens que o servidor envia sem aguardar _acknowledgment_.
    
- **`global`**: se `false`, a configuração se aplica por **consumidor**; se `true`, se aplica por canal como um todo.

## Variações

1. **`prefetchCount = 1`**: Força round-robin estrito (cada consumidor processa uma mensagem de cada vez).
    
2. **`prefetchCount > 1`**: O consumidor pode “puxar” várias mensagens, o que pode ser mais rápido se o processo de ACK for demorado. No entanto, pode ocorrer algum desbalanceamento se o valor for muito alto.
    
3. **Combinação com `global = true`**: Aplica as regras de prefetch ao canal inteiro; pode ser útil quando há vários consumidores no mesmo canal, mas normalmente `false` é mais flexível (configuração por consumidor).

## Reflexões e Boas Práticas

- **Equilíbrio**: usar `prefetchCount = 1` garante o balanceamento, mas pode reduzir throughput se seu processamento for rápido. Ajuste esse valor de acordo com a taxa de mensagens e tempo de processamento de cada _worker_.
    
- **Manuseio de Falhas**: com `autoAck = false`, se um _worker_ cair sem confirmar as mensagens, elas **voltam à fila** automaticamente, permitindo que outro consumidor as processe.
    
- **Teste de Carga**: é fundamental fazer _load tests_ para descobrir o **prefetchCount** ideal, balanceando throughput e risco de desbalanceamento.
    
- **Situação com Mensagens em Lote**: se o consumidor precisar de eficiência em _batch_, pode ser útil aumentar o `prefetchCount` para processar várias mensagens de uma vez, desde que gerencie corretamente o ACK.

## Conclusão

O uso do **`BasicQos`** e do **prefetchCount** faz toda a diferença para um sistema com múltiplos consumidores (ou mesmo um consumidor que precisa evitar “puxar” todas as mensagens de uma só vez). A chave é encontrar o ponto de equilíbrio entre **distribuição eficaz** das mensagens e **alto desempenho**.
