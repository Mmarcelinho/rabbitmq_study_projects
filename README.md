# RabbitMQ Tutorials

Este repositório contém uma série de tutoriais progressivos para aprender RabbitMQ com C#/.NET. Cada tutorial é organizado em um diretório independente com exemplos práticos e explicações detalhadas.

## Estrutura dos Tutoriais

### Demo01 - Conceitos Básicos
- Introdução ao RabbitMQ
- Produtor e Consumidor básicos
- Conceitos fundamentais de filas

### Demo02 - ASP.NET Core e Worker
- Integração com ASP.NET Core
- Worker Service
- Durabilidade e persistência

### Demo03 - Round-Robin
- Distribuição de mensagens entre múltiplos consumidores
- Balanceamento automático

### Demo04 - Conexões e Canais
- Gerenciamento de conexões e canais
- Escalabilidade e isolamento

### Demo05 - ACK Manual
- Confirmação manual de mensagens
- Tratamento de erros e reprocessamento

### Demo06 - BasicQos (Prefetch)
- Controle de fluxo de mensagens
- Balanceamento de carga

### Demo07 - Exchange Fanout
- Broadcast de mensagens
- Replicação para múltiplas filas

### Demo08 - Exchange Direct
- Roteamento por routing key exata
- Filtros específicos

### Demo09 - Exchange Topic
- Roteamento por padrões
- Wildcards (* e #)

### Demo10 - Exchange Headers
- Roteamento por cabeçalhos
- Flexibilidade máxima

### Demo11 - Dead Letter Queue (DLQ)
- Tratamento de mensagens com erro
- Evitar loops infinitos

### Demo12 - Time To Live (TTL)
- Expiração de mensagens
- Controle de tempo de vida

### Demo13 - Persistência
- Durabilidade de filas e mensagens
- Recuperação após reinicialização

### Demo14 - Publisher Confirms
- Confirmação de publicação
- Controle de fluxo do produtor

### Demo15 - RPC (Remote Procedure Call)
- Comunicação síncrona assíncrona
- Padrão request-response

## Padronização dos READMEs

Todos os READMEs foram padronizados seguindo um formato consistente:

### Estrutura Padrão
- **Título principal**: Nome do conceito + "no RabbitMQ"
- **Introdução**: Contexto e objetivos
- **Seções organizadas**: Conceitos, exemplos, código, considerações
- **Separadores**: Uso consistente de `---` entre seções
- **Hierarquia**: Títulos com níveis apropriados (#, ##, ###, ####)

### Formatação Consistente
- **Código**: Blocos com especificação de linguagem (```csharp)
- **Listas**: Formatação uniforme com marcadores
- **Tabelas**: Alinhamento e formatação padronizados
- **Ênfase**: Uso consistente de **negrito** e *itálico*

### Validação de Conteúdo
✅ Todos os READMEs foram validados e estão alinhados com os códigos correspondentes
✅ Exemplos práticos funcionais
✅ Explicações claras e didáticas
✅ Boas práticas documentadas

## Tecnologias Usadas

![badge-windows]
![badge-vs-code]
![badge-c-sharp]
![badge-dot-net]
![badge-rabbitmq]

## Autores

Estes projetos de exemplo foram criados para fins educacionais. [Marcelo](https://github.com/Mmarcelinho) é responsável pela criação e manutenção destes projetos.

## Licença

Este projetos não possuem uma licença específica e são fornecidos apenas para fins de aprendizado e demonstração.

[badge-windows]: https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white
[badge-vs-code]: https://img.shields.io/badge/Visual%20Studio%20Code-0078d7.svg?style=for-the-badge&logo=visual-studio-code&logoColor=white
[badge-dot-net]: https://img.shields.io/badge/.NET-512BD4?logo=dotnet&logoColor=fff&style=for-the-badge
[badge-c-sharp]: https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=csharp&logoColor=white
[badge-rabbitmq]: https://img.shields.io/badge/rabbitmq-%23FF6600.svg?&style=for-the-badge&logo=rabbitmq&logoColor=white
