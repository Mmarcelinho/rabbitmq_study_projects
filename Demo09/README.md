# Exchange Topic no RabbitMQ

## Introdução

O **Topic Exchange** do RabbitMQ é um tipo avançado de exchange que permite rotear mensagens para filas com base em padrões flexíveis definidos através de **routing keys**.

Diferentemente do **Fanout Exchange** (que copia mensagens para todas as filas) e do **Direct Exchange** (que utiliza chaves exatas para roteamento), o **Topic Exchange** oferece um mecanismo intermediário, permitindo rotear mensagens através de **padrões de texto** utilizando caracteres especiais.

---

## Principais Características do Topic Exchange

- A **routing key** precisa ser composta por palavras separadas por pontos (`.`).

- Suporta **wildcards** para definir padrões:
  - `*` (asterisco): substitui exatamente **uma** palavra.
  - `#` (hash): substitui **zero ou mais** palavras.

---

## Exemplo

Imagine uma empresa que possui a seguinte hierarquia:

```
          Diretoria (finance)
              |
     -------------------
     |                 |
 Coord. SP          Coord. RJ
     |                 |
------------      ------------
|          |      |          |
SP1       SP2     RJ1        RJ2
```

### Objetivo

Permitir enviar mensagens para níveis específicos ou grupos de níveis usando padrões flexíveis.

### Exemplo de Routing Keys

- `finance` → Mensagens para Diretoria.
- `finance.sp` → Apenas Coordenação SP.
- `finance.rj.rj1` → Apenas Supervisão RJ1.

### Configuração dos Bindings (no RabbitMQ UI)

| Fila | Binding Key | Explicação |
|------|-------------|------------|
| Diretoria | `finance.#` | Recebe todas mensagens iniciadas por "finance" |
| Coord SP | `finance.sp.*` | Recebe mensagens para SP1, SP2, etc. |
| Coord RJ | `finance.rj.*` | Recebe mensagens para RJ1, RJ2, etc. |
| SP1 | `finance.sp.sp1` | Apenas mensagens específicas SP1 |
| SP2 | `finance.sp.sp2` | Apenas mensagens específicas SP2 |
| RJ1 | `finance.rj.rj1` | Apenas mensagens específicas RJ1 |
| RJ2 | `finance.rj.rj2` | Apenas mensagens específicas RJ2 |

---

## Exemplo Prático em .NET

### Publicador (.NET)

```csharp
var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Cria exchange tipo Topic
await channel.ExchangeDeclareAsync("topic_logs", ExchangeType.Topic);

// Exemplo prático de envio de mensagem:
var routingKey = "finance.rj.rj1";
var message = "Mensagem específica para RJ1";
var body = Encoding.UTF8.GetBytes(message);

await channel.BasicPublishAsync(
    exchange: "topic_logs",
    routingKey: routingKey,
    body: body);

Console.WriteLine($"[x] Enviada '{routingKey}':'{message}'");
```

### Consumidor (.NET)

```csharp
var factory = new ConnectionFactory { HostName = "localhost" };

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

await channel.ExchangeDeclareAsync("topic_logs", ExchangeType.Topic);

// Cria fila anônima (gerada automaticamente)
var queueDeclareResult = await channel.QueueDeclareAsync();
string queueName = queueDeclareResult.QueueName;

// Binding keys (escuta múltiplos padrões, exemplo: todas mensagens do RJ)
string[] bindingKeys = new[] { "finance.rj.*" };

foreach (var bindingKey in bindingKeys)
{
    await channel.QueueBindAsync(queue: queueName, exchange: "topic_logs", routingKey: bindingKey);
}

Console.WriteLine("[*] Esperando mensagens (finance.rj.*)");

var consumer = new AsyncEventingBasicConsumer(channel);

consumer.ReceivedAsync += (model, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    var receivedRoutingKey = ea.RoutingKey;

    Console.WriteLine($"[x] Recebido '{receivedRoutingKey}':'{message}'");
    return Task.CompletedTask;
};

await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
Console.WriteLine(" Pressione [enter] para sair.");
Console.ReadLine();
```

---

## Exemplos de Padrões (Binding Keys)

Imagine uma routing key no formato:

```
<departamento>.<local>.<nível>
```

| Binding Key | Exemplo de Routing Key válida | Explicação |
|-------------|-------------------------------|------------|
| `finance.#` | `finance.sp.sp1`, `finance.rj.rj1` | Recebe mensagens de qualquer nível financeiro |
| `finance.*` | `finance.sp`, `finance.rj` | Recebe mensagens diretamente abaixo de finance |
| `*.rj.*` | `finance.rj.rj1`, `rh.rj.coord` | Recebe mensagens de RJ, independente do departamento |
| `#` | Todas | Recebe todas as mensagens (equivale ao fanout) |

---

## Comparação com Outros Exchanges

| Exchange | Uso | Flexibilidade |
|----------|-----|---------------|
| Fanout | Envia para todas as filas | Baixa |
| Direct | Envia usando routing key exata | Média |
| Topic | Envia usando padrões complexos | Alta |

O **Topic Exchange** é ideal para sistemas de logs, alertas e notificações complexas.

---

## Pontos Importantes

- Mensagens com routing keys que não atendem a nenhum padrão são descartadas.

- Utilize `*` para segmentação exata de palavras.

- Utilize `#` para escutar níveis variados (inclusive múltiplos níveis).

---

## Resumo Rápido

- **Topic Exchange** permite roteamento inteligente e flexível de mensagens.

- Usa padrões nas routing keys (com `*` e `#`) para definir quais filas recebem quais mensagens.

- É indicado para cenários onde mensagens precisam ser entregues de forma dinâmica e seletiva, com muita flexibilidade.
