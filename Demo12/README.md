# Time To Live (TTL) 

TTL define um tempo de expiração para mensagens em filas. Se a mensagem não for consumida dentro do tempo especificado, ela é automaticamente removida da fila.

## Dois tipos de TTL

### TTL por Mensagem (individual)

Define o tempo de vida no momento em que a mensagem é publicada.

```csharp
var props = channel.CreateBasicProperties();
props.Expiration = "10000"; // 10 segundos

await channel.BasicPublishAsync(
    exchange: "",
    routingKey: queueName,
    basicProperties: props,
    body: body
);
```

- A propriedade `Expiration` é definida em **milissegundos**.
    
- Esse TTL é aplicado **somente à mensagem enviada**, não afeta as demais.

### TTL por Fila (padrão para todas as mensagens)

Define um TTL padrão para todas as mensagens na fila.

```csharp
var args = new Dictionary<string, object>();
args.Add("x-message-ttl", 22000); // 22 segundos

await channel.QueueDeclareAsync(
    queue: "test_time_to_live",
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: args
);
```

- O argumento `x-message-ttl` é configurado na criação da fila.
    
- Todas as mensagens publicadas nesta fila terão o mesmo TTL, a menos que um TTL individual seja fornecido na mensagem.

---

## Quando Usar TTL?

TTL é útil quando:

- Você deseja garantir que mensagens antigas não sejam processadas.
    
- Você precisa evitar reprocessamento desnecessário.
    
- Mensagens antigas perdem o valor, como:
    
    - Atualizações de GPS.
        
    - Notificações de tempo real.
        
    - Solicitações de expiração (como tokens temporários).

---

## Funcionamento prático

- Se nenhuma **consumidor** processar a mensagem dentro do TTL, ela **desaparece** da fila.
    
- Isso evita loops desnecessários e uso indevido de recursos.
    
- Ao configurar TTL por fila, o RabbitMQ **não mostra a propriedade de expiração na mensagem** — o valor está atrelado à configuração da fila.

---

## Considerações

- TTL de mensagem **tem prioridade** sobre TTL da fila.
    
- A configuração de TTL em milissegundos deve ser precisa, respeitando o intervalo necessário para a lógica de negócio.
    
- TTL não garante ordenação nem confirmação de entrega — é uma estratégia de expiração passiva.
