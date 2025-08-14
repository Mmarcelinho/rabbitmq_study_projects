# Persistência no RabbitMQ

## Introdução

A persistência garante que as filas e mensagens permaneçam disponíveis mesmo após a reinicialização do servidor ou contêiner RabbitMQ. Isso é essencial para evitar perda de dados importantes durante operações comuns, como restart do servidor ou falhas inesperadas.

---

## Problema Comum

Ao reiniciar o servidor (container ou máquina física), pode ocorrer perda:

- Do próprio container Docker (se estiver rodando com o parâmetro `--rm`).
- Das filas criadas.
- Das mensagens que estavam nas filas.

---

## Evitando Perda do Container Docker

### Explicação

Ao executar o RabbitMQ no Docker, se você utilizar o comando com `--rm`, o container será **removido automaticamente** ao parar ou reiniciar, causando perda total do estado:

```bash
docker run --rm rabbitmq:latest
```

### Solução

Para persistir o container, basta remover o parâmetro `--rm`:

```bash
docker run rabbitmq:latest
```

Dessa forma, o container permanece disponível após o stop e pode ser iniciado novamente com:

```bash
docker start <nome_container>
docker stop <nome_container>
```

---

## Tornando Filas Persistentes

Por padrão, as filas são transitórias (`durable: false`) e desaparecem ao reiniciar o RabbitMQ. Para persistir filas, configure:

```csharp
await channel.QueueDeclareAsync(
    queue: "nome_da_fila",
    durable: true,     // importante!
    exclusive: false,
    autoDelete: false,
    arguments: null
);
```

A propriedade `durable: true` indica ao RabbitMQ para salvar a definição da fila em disco, garantindo que ela seja recuperada após reinicialização do servidor.

---

## Tornando Mensagens Persistentes

Mesmo com a fila durável, as mensagens são transitórias por padrão. Para persistir mensagens, você precisa configurar uma propriedade adicional nas mensagens:

```csharp
var properties = channel.CreateBasicProperties();
properties.Persistent = true;  // mensagem persistente

await channel.BasicPublishAsync(
    exchange: "",
    routingKey: "nome_da_fila",
    basicProperties: properties,
    body: mensagem
);
```

Com essa configuração (`Persistent = true`), as mensagens são armazenadas em disco, evitando perda em reinicializações.

---

## Fluxo Completo de Persistência

Ao implementar corretamente, temos:

- Container **persistente** (remover `--rm`).
- Fila **persistente** (`durable: true`).
- Mensagens **persistentes** (`Persistent = true`).

Com essa configuração completa, mesmo após restart do container, tudo se mantém intacto.

---

## Trade-off (Impacto na Performance)

- Filas e mensagens persistentes têm impacto no desempenho, com redução média de **até 20%** devido à gravação em disco.
- Recomenda-se utilizar discos rápidos (SSD) para reduzir essa latência adicional.

---

## Exemplo Completo

```csharp
var factory = new ConnectionFactory { HostName = "localhost" };

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Criação da fila persistente (durável)
await channel.QueueDeclareAsync(
    queue: "fila_duravel",
    durable: true,        // Fila persistente
    exclusive: false,
    autoDelete: false,
    arguments: null
);

string mensagem = "Minha mensagem persistente";
var body = Encoding.UTF8.GetBytes(mensagem);

// Propriedade de persistência da mensagem
var props = channel.CreateBasicProperties();
props.Persistent = true;

// Publicação da mensagem persistente
await channel.BasicPublishAsync(
    exchange: "",
    routingKey: "fila_duravel",
    basicProperties: props,
    body: body
);
```
