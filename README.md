# RabbitMQ Tutorials

Este repositório contém uma série de tutoriais progressivos para aprender RabbitMQ com C#/.NET. Cada tutorial é organizado em um diretório independente com exemplos práticos e explicações detalhadas.

## Estrutura do Projeto

- **Demo01**: Introdução ao RabbitMQ - Conceitos básicos de Produtor e Consumidor
- **Demo02**: Sistema de Pedidos Durável - API ASP.NET Core com Worker em background
- **Demo03**: Comunicação entre Produtor e Consumidor com padrões avançados
- **Demo04**: Padrão Publish/Subscribe e Exchange types (Direct, Topic, Fanout)
- **Demo05**: ACK Manual (Manual Acknowledgment) - Recuperação de falhas e resiliência
- **Demo06**: RPC (Remote Procedure Call) com RabbitMQ
- **Demo07**: Estratégias de roteamento avançadas
- **Demo08**: Padrões de Integração Empresarial

## Requisitos

- [.NET 6 ou superior](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop) (para executar RabbitMQ)
- Visual Studio 2022 ou outro IDE compatível com .NET

## Como Executar o RabbitMQ

Você pode facilmente executar o RabbitMQ em um contêiner Docker:

```bash
docker run -d --hostname my-rabbit --name rabbitmq-container -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

Acesse o Management UI em: http://localhost:15672  
Login padrão: guest/guest

## Principais Conceitos Abordados

1. **Produtores e Consumidores**: Criação de aplicações para enviar e receber mensagens
2. **Filas e Exchanges**: Tipos diferentes (Direct, Topic, Fanout, Headers) e seus usos
3. **Durabilidade e Persistência**: Configurando mensagens para persistir em caso de falhas
4. **Acknowledgments**: Confirmação manual de mensagens para maior confiabilidade
5. **Padrões de Mensageria**: Work Queues, Pub/Sub, RPC, Routing
6. **Escalabilidade**: Múltiplos consumidores e balanceamento de carga
7. **Tratamento de Erros**: Retentativas, dead-letter queues e estratégias de recuperação

## Como Usar os Tutoriais

Cada diretório contém:
- Um arquivo README.md detalhado explicando os conceitos
- Código-fonte completo em C#/.NET
- Instruções de execução e experimentação

Para seguir os tutoriais:

1. Clone este repositório
2. Navegue para o diretório do tutorial desejado
3. Leia o README.md para entender os conceitos
4. Execute o código seguindo as instruções

## Progressão Recomendada

Para uma melhor experiência de aprendizado, recomendamos seguir os tutoriais na ordem numérica, do Demo01 ao Demo08, pois cada tutorial constrói conhecimento sobre o anterior.

## Recursos Adicionais

- [Documentação Oficial do RabbitMQ](https://www.rabbitmq.com/documentation.html)
- [Cliente .NET para RabbitMQ](https://www.rabbitmq.com/dotnet.html)
- [Padrões de Mensageria Empresarial](https://www.enterpriseintegrationpatterns.com/) 