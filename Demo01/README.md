# RabbitMQ - Conceitos Básicos

## Introdução

O RabbitMQ é um broker de mensagens que atua como intermediário no envio e recebimento de dados (mensagens) entre aplicações. Ele facilita a comunicação assíncrona, permitindo que uma ou mais aplicações (produtores) publiquem mensagens em filas, enquanto uma ou mais aplicações (consumidores) leem e processam essas mensagens independentemente.

### Componentes Principais

- **Produtor (Producer)**: A aplicação que envia (publica) mensagens para uma fila.
- **Consumidor (Consumer)**: A aplicação que recebe (consome) as mensagens de uma fila.
- **Fila (Queue)**: Local onde as mensagens ficam armazenadas até serem consumidas.
- **Broker**: O serviço (RabbitMQ) responsável por receber as mensagens dos produtores e entregá-las aos consumidores.

Essa arquitetura é bastante utilizada para **desacoplar** sistemas, ou seja, para que a capacidade de produção de mensagens não seja limitada pela capacidade de consumo e processamento, permitindo escalabilidade e maior confiabilidade.

---

## Exemplo de Código em .NET (C#)

A seguir, apresentamos dois códigos simples em C# que demonstram o envio e recebimento de mensagens usando RabbitMQ. Um projeto funciona como **Produtor** e o outro como **Consumidor**.

### Produtor (Publisher)

```csharp
namespace RProducer;

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
            queue: "qwebapp",
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
            string message = $"Hello World! - Count: {count++}";
            var body = Encoding.UTF8.GetBytes(message);

            // Publica a mensagem na fila "qwebapp"
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "qwebapp",
                body: body
            );

            Console.WriteLine($" [x] Sent {message}");

            // Pequeno delay para visualizar melhor o envio
            Thread.Sleep(200);
        }
    }
}
```

#### Observações Importantes

1. **ConnectionFactory**: Centraliza as informações de conexão (host, porta, credenciais, etc.).
2. **CreateConnectionAsync** e **CreateChannelAsync**: Abrem a conexão com o RabbitMQ e criam um canal para a comunicação.
3. **QueueDeclareAsync**: Garante que a fila exista antes de publicar as mensagens.
4. **BasicPublishAsync**: Envia efetivamente a mensagem para a fila, utilizando a _routingKey_ que corresponde ao nome da fila (quando o `exchange` está vazio, o RabbitMQ usa o _default exchange_).
5. **while (true)**: Mantém o envio de mensagens contínuo, para fins de teste. Em produção, o envio costuma ocorrer em pontos específicos da aplicação.

---

### Consumidor (Consumer)

```csharp
namespace RConsumer;

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
            queue: "qwebapp",
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
        await channel.BasicConsumeAsync(
            queue: "qwebapp",
            autoAck: true,
            consumer: consumer
        );

        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();
    }
}
```

#### Observações Importantes

1. **QueueDeclareAsync**: Mais uma vez, garante que a fila exista. Embora o produtor também declare a fila, é seguro que o consumidor também a declare, pois não sabemos qual aplicação iniciará primeiro.
2. **AsyncEventingBasicConsumer**: Consumidor que funciona de forma assíncrona. Sempre que chega uma mensagem, o evento `ReceivedAsync` é disparado.
3. **BasicConsumeAsync**: Inicia efetivamente o consumo das mensagens na fila informada (`qwebapp`).
4. **autoAck = true**: Indica que as mensagens são consideradas confirmadas automaticamente. Em cenários de produção, você pode configurar o _ack_ manual para garantir que uma mensagem só seja removida da fila após ser processada com sucesso.

---

## Execução dos Projetos

Para executar o exemplo:

1. **Inicie o serviço RabbitMQ** na sua máquina ou em um servidor.
   - Por padrão, o RabbitMQ fica disponível na porta 5672, e a interface de administração, se habilitada, na porta 15672.

2. **Compile e execute** o projeto do **Consumidor (RConsumer)**.

3. **Compile e execute** o projeto do **Produtor (RProducer)**.

Ao executar o **Consumidor**, você deve ver a mensagem:

```
 [*] Waiting for messages.
 Press [enter] to exit.
```

Em seguida, ao iniciar o **Produtor**, ele começará a enviar mensagens, e o consumidor, por sua vez, exibirá no console cada mensagem recebida:

```
 [x] Sent Hello World! - Count: 0
 [x] Sent Hello World! - Count: 1
 ...
```

Do outro lado:

```
 [x] Received Hello World! - Count: 0
 [x] Received Hello World! - Count: 1
 ...
```

---

## Pontos de Atenção e Boas Práticas

### Durabilidade (durable) e Persistência

- Para ambientes de produção, é comum marcar a fila como durável e também configurar as mensagens como persistentes para evitar perda em caso de reinício do broker.

### Confirmações de Mensagem (Acknowledgements)

- Ao usar `autoAck = true`, a mensagem é considerada entregue assim que chega no consumidor. Em situações críticas, convém usar `autoAck = false` e dar o _ack_ manualmente (ex.: `channel.BasicAck`) somente depois de processar a mensagem com sucesso.

### Tratamento de Erros

- Caso ocorra uma exceção durante o processamento, podemos utilizar o _Negative Acknowledge_ (`BasicNack`) para retornar a mensagem à fila (ou descartá-la, dependendo da estratégia).

### Exchange

- No exemplo acima, usamos o _default exchange_ (string vazia) com `routingKey` igual ao nome da fila. Em cenários avançados, podemos configurar _exchanges_ personalizadas (tipo _direct_, _topic_, _fanout_, etc.) para mais flexibilidade e roteamento complexo.

### Escalabilidade

- Com RabbitMQ, podemos ter múltiplos consumidores lendo de uma mesma fila para paralelizar o processamento. Também podemos ter múltiplos produtores publicando em alta escala.

### Monitoramento

- Usar o **Management Plugin** do RabbitMQ (normalmente em `http://localhost:15672`) para acompanhar o status das filas, mensagens pendentes, conexões e muito mais.

---

## Conclusão

O RabbitMQ é uma ferramenta poderosa para comunicação assíncrona e troca de mensagens entre aplicações, fornecendo desacoplamento e escalabilidade. Com poucos trechos de código em C#, é possível configurar de maneira simples o envio (Produtor) e o recebimento (Consumidor) de mensagens, garantindo maior robustez na sua arquitetura.

