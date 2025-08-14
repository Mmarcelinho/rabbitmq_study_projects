# Publisher Confirms no RabbitMQ

## Introdução

O mecanismo de **Publisher Confirms** no RabbitMQ permite ao produtor (publisher) saber se a mensagem foi de fato aceita pelo broker. Isso aumenta o controle e confiabilidade da aplicação, pois o produtor pode reagir conforme a confirmação de sucesso (Ack) ou falha (Nack).

Sem o uso de Publisher Confirms, o publisher apenas "envia" a mensagem, sem saber se ela foi entregue ou roteada corretamente. Com esse mecanismo ativado, é possível:

- Confirmar se o RabbitMQ processou a mensagem com sucesso (Ack).
- Detectar se a mensagem falhou no processamento (Nack).
- Detectar erros de roteamento com eventos de retorno (Return).
- Adotar lógica de retry, fallback, logging ou outras estratégias com base nas confirmações.

---

## Ativando Publisher Confirms

Antes de publicar mensagens, é necessário ativar o modo de confirmação no canal:

```csharp
channel.ConfirmSelect();
```

Esse comando habilita o canal para operar no modo de confirmação. Ele só precisa ser chamado uma vez após a criação do canal.

---

## Eventos Importantes

Registre os eventos para capturar os feedbacks:

```csharp
channel.BasicAcks += (sender, ea) =>
{
    Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Ack");
};

channel.BasicNacks += (sender, ea) =>
{
    Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Nack");
};

channel.BasicReturn += (sender, ea) =>
{
    var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Return -> Mensagem: {msg}");
};
```

- `BasicAcks`: confirma que o broker aceitou a mensagem.
- `BasicNacks`: indica falha de entrega/processamento.
- `BasicReturn`: ocorre quando a mensagem não pôde ser roteada e foi devolvida (em conjunto com o parâmetro `mandatory = true`).

---

## Publicando Mensagem com Confirmação e Timeout

A publicação com confirmação e espera por `Ack/Nack` pode ser feita assim:

```csharp
var factory = new ConnectionFactory() { HostName = "localhost" };

await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateModelAsync();

await channel.ConfirmSelectAsync();

channel.BasicAcks += (s, e) =>
{
    Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Ack");
};

channel.BasicNacks += (s, e) =>
{
    Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Nack");
};

channel.BasicReturn += (s, e) =>
{
    var message = Encoding.UTF8.GetString(e.Body.ToArray());
    Console.WriteLine($"{DateTime.UtcNow:o} -> Basic Return -> Mensagem: {message}");
};

// Criação da fila
await channel.QueueDeclareAsync(
    queue: "orders",
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: null
);

// Publicação
var message = $"{DateTime.UtcNow:o} -> Hello World!";
var body = Encoding.UTF8.GetBytes(message);

var props = channel.CreateBasicProperties();
props.Persistent = true;

channel.BasicPublish(
    exchange: "",
    routingKey: "orders",
    mandatory: true,
    basicProperties: props,
    body: body
);

// Aguarda confirmação com timeout de 5 segundos
if (await channel.WaitForConfirmsAsync(TimeSpan.FromSeconds(5)))
{
    Console.WriteLine($"[x] Mensagem publicada com sucesso: {message}");
}
else
{
    Console.WriteLine($"[!] Timeout ao aguardar confirmação da mensagem: {message}");
}
```

---

## Tratando Erros de Roteamento

Se a mensagem for enviada com um `routingKey` que não possui fila associada, e `mandatory = true`, o evento `BasicReturn` será chamado, permitindo capturar o erro e tratar adequadamente:

```csharp
channel.BasicPublish(
    exchange: "",
    routingKey: "fila_que_nao_existe",
    mandatory: true,
    basicProperties: props,
    body: body
);
```

Isso ajuda a evitar o descarte silencioso de mensagens.

---

## Estratégias Avançadas

1. **Retry com delay**: ao receber Nack ou retorno, pode-se usar uma fila de retry com TTL para reprocessamento posterior.

2. **Fallback manual**: enviar a mensagem para uma fila de fallback ou registrar em log.

3. **Monitoramento de saúde**: gerar alerta se o canal for desconectado ou se mensagens forem rejeitadas em massa.

4. **Canal desconectado**: se ocorrer falha de conexão, deve-se reconectar e reestabelecer o canal.

---

## Boas Práticas

- Sempre tratar os eventos `BasicAck`, `BasicNack` e `BasicReturn`.
- Usar `mandatory = true` ao publicar, para capturar falhas de roteamento.
- Evite depender unicamente do `WaitForConfirmsAsync`, pois mensagens podem ser devolvidas silenciosamente.
- Use logs para rastrear mensagens que falharam.

