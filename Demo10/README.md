# Exchange do Tipo Headers

## Conceito Geral

O Exchange do tipo **Headers** realiza o roteamento de mensagens com base em **headers (cabeçalhos)** definidos na mensagem, ao invés de utilizar a tradicional **Routing Key** como nos exchanges dos tipos Direct, Topic ou Fanout.

Este tipo é extremamente útil em cenários que exigem maior flexibilidade, especialmente quando você precisa distribuir mensagens com base em atributos ou propriedades específicas (chave-valor).

---

## Funcionamento

Diferentemente dos exchanges anteriores:

- **Fanout:** envia cópia da mensagem para todas as filas vinculadas.
    
- **Direct:** envia mensagem com base em uma chave exata (Routing Key).
    
- **Topic:** envia mensagem com base em padrões definidos na Routing Key.

O exchange **Headers**:

- Não utiliza Routing Key diretamente.
    
- Utiliza um conjunto de headers (cabeçalhos) no formato **chave-valor**.
    
- É mais flexível para roteamento baseado em critérios complexos.
    

**Características principais:**

- Cabeçalhos são definidos na mensagem.
    
- Cada fila é ligada (binding) ao exchange através de cabeçalhos específicos.
    
- As mensagens são roteadas para filas que possuem cabeçalhos compatíveis.

---

## Exemplo Prático: Cenário de Negócio

Imagine um cenário com três setores diferentes:

- **Financeiro**
    
- **Contabilidade**
    
- **Logística**

Você deseja enviar mensagens específicas para cada setor usando headers:

- Mensagens com header `setor = financeiro` vão para a fila **financeiro**.
    
- Mensagens com header `setor = contabilidade` vão para a fila **contabilidade**.
    
- Mensagens com header `setor = logistica` vão para a fila **logistica**.
    
- Todas as mensagens do setor **financeiro** devem ser copiadas também para uma fila adicional chamada **auditoria**.

---

## Configuração no RabbitMQ Management

##  Criar Exchange Headers

- **Exchange name:** `business_exchange`
    
- **Type:** `headers`
    

## Criar Filas

Crie as filas:

- financeiro
    
- contabilidade
    
- logistica
    
- auditoria

## Binding com Headers

Exemplo de bindings (associação entre Exchange e Fila):

|Fila|Header (Argumento)|Valor|
|---|---|---|
|financeiro|setor|financeiro|
|contabilidade|setor|contabilidade|
|logistica|setor|logistica|
|auditoria|setor|financeiro|

**Observação:**

- Quando uma mensagem chega com `setor=financeiro`, ela vai para as filas `financeiro` **e também** `auditoria`.

---

## Demo em C# com .NET (Produtor e Consumidor)

### Produtor

```csharp
var factory = new ConnectionFactory { HostName = "localhost" };

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Declara Exchange tipo Headers
await channel.ExchangeDeclareAsync("business_exchange", ExchangeType.Headers);

// Exemplo de envio da mensagem com header setor=financeiro
var properties = channel.CreateBasicProperties();
properties.Headers = new Dictionary<string, object>
{
    { "setor", "financeiro" }
};

string mensagem = "Nova mensagem para setor financeiro";
var body = Encoding.UTF8.GetBytes(mensagem);

// Publica no Exchange headers
await channel.BasicPublishAsync(
    exchange: "business_exchange",
    routingKey: "",  // Não usado em Headers
    basicProperties: properties,
    body: body
);

Console.WriteLine($"Enviada mensagem com header setor=financeiro");
```

### Consumidor

```csharp
var factory = new ConnectionFactory { HostName = "localhost" };

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Fila para consumir
string queueName = "financeiro";

await channel.QueueDeclareAsync(queue: queueName,
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

// Binding com Header (setor=financeiro)
await channel.QueueBindAsync(queue: queueName,
                             exchange: "business_exchange",
                             routingKey: "",  // Não usado em Headers
                             arguments: new Dictionary<string, object> { { "setor", "financeiro" } });

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += (model, ea) =>
{
    var body = ea.Body.ToArray();
    var mensagem = Encoding.UTF8.GetString(body);

    Console.WriteLine($"Recebido no setor financeiro: {mensagem}");

    channel.BasicAck(ea.DeliveryTag, false);
    return Task.CompletedTask;
};

await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

Console.WriteLine("Pressione [ENTER] para sair");
Console.ReadLine();
```

---

## Resultado Esperado da Demo

- Ao enviar mensagens com header `setor=financeiro`, as filas **financeiro** e **auditoria** recebem a mensagem.
    
- Ao enviar mensagens com header `setor=contabilidade`, apenas a fila **contabilidade** recebe a mensagem.
    
- Ao enviar mensagens com header `setor=logistica`, apenas a fila **logistica** recebe a mensagem.

Isso ocorre porque o exchange Headers roteia mensagens de acordo com os cabeçalhos definidos na mensagem e nos bindings das filas.

---

## Principais Casos de Uso

- Sistemas de notificação (e-mail, SMS, push) baseados em propriedades dinâmicas.
    
- Processamento específico de arquivos/imagens com diferentes formatos (jpeg, pdf, png).
    
- Aplicações que precisam de flexibilidade na definição das regras de roteamento das mensagens (mapeamento dinâmico via HTTP headers).

---

## Pontos Importantes

- Exchange do tipo Headers ignora a Routing Key tradicional.
    
- Roteia exclusivamente com base nos cabeçalhos (headers).
    
- Muito flexível e poderosa para regras complexas.
