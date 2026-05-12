## PROMPT PARA O OUTRO MICROSERVIÇO - COPIAR E COLAR NO COPILOT

---

Analise este repositório de referência (ProcessadorDiagramas.APIGatewayService) e adapte o padrão de Message Bus para nosso microserviço (processador-diagramas-processingservice).

### CONTEXTO DO PROBLEMA
Nosso microserviço está recebendo erros de autorização AWS com a política `voc-cancel-cred` negando:
- SNS:Publish (crítico - deve estar permitido)
- SNS:Subscribe (correto estar negado, mas código está chamando)
- SNS:SetSubscriptionAttributes (correto estar negado, mas código está chamando)
- SQS:PurgeQueue (correto estar negado, nunca deveria chamar)

**SNS Topic:** arn:aws:sns:us-east-1:767398027345:processador-diagramas-processingservice-hml-topic
**SQS Queue:** arn:aws:sqs:us-east-1:767398027345:processador-diagramas-processingservice-hml-queue

### PADRÃO DE REFERÊNCIA - ESTE REPOSITÓRIO
Repositório: https://github.com/marciocardozodev-org/ProcessadorDiagramas.ApiGatewayService (branch: develop)

Arquivos chave para análise:
1. `src/ProcessadorDiagramas.APIGatewayService/Infrastructure/Messaging/AwsMessageBus.cs` - Implementação de publish/subscribe sem provisionar recursos em runtime
2. `src/ProcessadorDiagramas.APIGatewayService/Inbox/InboxConsumer.cs` - Consumer que NÃO provisiona infraestrutura
3. `src/ProcessadorDiagramas.APIGatewayService/Outbox/OutboxPublisher.cs` - Publisher que NÃO provisiona infraestrutura
4. `src/ProcessadorDiagramas.APIGatewayService/Program.cs` (linhas 51-66) - Registro condicional de AwsMessageBus/DummyMessageBus
5. `src/ProcessadorDiagramas.APIGatewayService/appsettings.Development.json` - Apontando para LocalStack
6. `scripts/init-localstack.sh` (linhas 70-130) - Provisão uma única vez: sns create-topic, sqs create-queue, aws sns subscribe

### TAREFA 1: Criar Abstração de Message Bus
Implemente uma interface `IMessageBus` idêntica ao padrão do repositório de referência com:
- Método `Task PublishAsync(string eventType, string payload)` que chama APENAS sns:Publish
- Método `Task SubscribeAsync(Func<BusMessage, Task> handler, CancellationToken cancellationToken)` que faz APENAS sqs:ReceiveMessage + sqs:DeleteMessage
- Nenhuma chamada para sns:Subscribe, sns:SetSubscriptionAttributes, sns:CreateTopic, sqs:CreateQueue, sqs:PurgeQueue em tempo de execução

Implemente 3 versões:
1. `AwsMessageBus` - usando IAmazonSimpleNotificationService e IAmazonSQS do SDK AWS
2. `DummyMessageBus` - apenas loga, não faz nada (para testes unitários)
3. `LocalStackMessageBus` - aponta para LocalStack endpoints (para testes E2E sem AWS credentials)

### TAREFA 2: Refatorar Program.cs
Adicione registro condicional que:
- Se `EnableAwsServices` for true: registra AwsMessageBus, InboxConsumer, OutboxWorker
- Se `EnableAwsServices` for false: registra DummyMessageBus apenas
- Deve permitir desativar AWS services em desenvolvimento adicionando no appsettings.Development.json: `"EnableAwsServices": false`

### TAREFA 3: Criar Script de Provisão de Infraestrutura
Crie `scripts/init-aws-resources.sh` que:
- Cria SNS Topic: processador-diagramas-processingservice-hml-topic
- Cria SQS Queue: processador-diagramas-processingservice-hml-queue
- Chama `aws sns subscribe` para inscrever fila ao tópico
- Define políticas de acesso com `aws sqs set-queue-attributes`
- Deve rodar UMA ÚNICA VEZ em contexto administrativo (fora do ciclo da aplicação)

### TAREFA 4: Validar Código Atual
Procure no codebase por e REMOVA qualquer linha que:
- Chame `sns.Subscribe(...)`
- Chame `sns.CreateTopic(...)`
- Chame `sns.SetSubscriptionAttributes(...)`
- Chame `sqs.CreateQueue(...)`
- Chame `sqs.SetQueueAttributes(...)`
- Chame `sqs.PurgeQueue(...)`

Essas operações NUNCA devem estar em tempo de execução. Devem estar APENAS no script `init-aws-resources.sh`.

### RESULTADO ESPERADO
Ao final, o código da aplicação deve chamar APENAS estas operações AWS em tempo de runtime:
- ✅ sns:Publish (no método PublishAsync)
- ✅ sqs:ReceiveMessage (no método SubscribeAsync)
- ✅ sqs:DeleteMessage (no método SubscribeAsync após processar message)

E o arquivo de política `voc-cancel-cred` deve ter APENAS essas 3 operações Allow. Operações de infraestrutura (Subscribe, CreateTopic, SetSubscriptionAttributes, PurgeQueue) nunca serão chamadas em runtime.

---

## CÓDIGO DE EXEMPLO - ESTRUTURA BASE

### 1. Interface IMessageBus
```csharp
namespace ProcessadorDiagramas.ProcessingService.Domain.Interfaces;

public interface IMessageBus
{
    Task PublishAsync(string eventType, string payload);
    Task SubscribeAsync(Func<BusMessage, Task> handler, CancellationToken cancellationToken);
}

public class BusMessage
{
    public string EventType { get; set; }
    public string Payload { get; set; }
}
```

### 2. AwsMessageBus (APENAS Publish + Receive)
```csharp
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public class AwsMessageBus : IMessageBus
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonSQS _sqs;
    private readonly AwsSettings _settings;
    private readonly ILogger<AwsMessageBus> _logger;

    public AwsMessageBus(
        IAmazonSimpleNotificationService sns,
        IAmazonSQS sqs,
        IOptions<AwsSettings> settings,
        ILogger<AwsMessageBus> logger)
    {
        _sns = sns;
        _sqs = sqs;
        _settings = settings.Value;
        _logger = logger;
    }

    // ✅ Runtime: APENAS Publish
    public async Task PublishAsync(string eventType, string payload)
    {
        try
        {
            var request = new PublishRequest
            {
                TopicArn = _settings.TopicArn,
                Message = payload,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["eventType"] = new MessageAttributeValue { StringValue = eventType, DataType = "String" }
                }
            };

            await _sns.PublishAsync(request);
            _logger.LogInformation("Message published to SNS topic: {TopicArn}", _settings.TopicArn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to SNS");
            throw;
        }
    }

    // ✅ Runtime: APENAS ReceiveMessage + DeleteMessage
    public async Task SubscribeAsync(Func<BusMessage, Task> handler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _settings.QueueUrl,
                    MaxNumberOfMessages = 1,
                    WaitTimeSeconds = 20, // Long polling
                    MessageAttributeNames = new List<string> { "All" }
                }, cancellationToken);

                foreach (var message in response.Messages)
                {
                    try
                    {
                        var busMessage = ExtractPayload(message.Body);
                        await handler(busMessage);

                        // ✅ Delete after successful processing
                        await _sqs.DeleteMessageAsync(_settings.QueueUrl, message.ReceiptHandle, cancellationToken);
                        _logger.LogInformation("Message processed and deleted from queue");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue");
                        // Message will be retried after visibility timeout
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Message subscription cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving messages from SQS");
                await Task.Delay(5000, cancellationToken); // Retry after delay
            }
        }
    }

    private BusMessage ExtractPayload(string sqsBody)
    {
        try
        {
            // SNS wraps message in JSON envelope when routing to SQS
            var document = System.Text.Json.JsonDocument.Parse(sqsBody);
            var root = document.RootElement;

            if (root.TryGetProperty("Message", out var messageElement))
            {
                var message = messageElement.GetString();
                var payloadDocument = System.Text.Json.JsonDocument.Parse(message);
                var payloadRoot = payloadDocument.RootElement;

                var eventType = payloadRoot.TryGetProperty("eventType", out var eventTypeElement)
                    ? eventTypeElement.GetString()
                    : "Unknown";

                return new BusMessage
                {
                    EventType = eventType,
                    Payload = message
                };
            }

            return new BusMessage { EventType = "Unknown", Payload = sqsBody };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to extract payload from SQS message", ex);
        }
    }
}
```

### 3. DummyMessageBus (para testes)
```csharp
using Microsoft.Extensions.Logging;

namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public class DummyMessageBus : IMessageBus
{
    private readonly ILogger<DummyMessageBus> _logger;

    public DummyMessageBus(ILogger<DummyMessageBus> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(string eventType, string payload)
    {
        _logger.LogInformation("[DUMMY] Would publish event type '{EventType}' with payload: {Payload}", eventType, payload);
        return Task.CompletedTask;
    }

    public async Task SubscribeAsync(Func<BusMessage, Task> handler, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DUMMY] Dummy message bus does not subscribe to any real messages");
        await Task.CompletedTask;
    }
}
```

### 4. AwsSettings (Configuração)
```csharp
namespace ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

public class AwsSettings
{
    public string Region { get; set; }
    public string TopicArn { get; set; }
    public string QueueUrl { get; set; }
    public string ServiceUrl { get; set; } // Para LocalStack
}
```

### 5. Program.cs (Registro Condicional)
```csharp
using Amazon;
using ProcessadorDiagramas.ProcessingService.Infrastructure.Messaging;

var builder = WebApplicationBuilder.CreateBuilder(args);

// ... outras configurações ...

var enableAwsServices = !builder.Environment.IsDevelopment() || 
    builder.Configuration.GetValue<bool>("EnableAwsServices", false);

if (enableAwsServices)
{
    builder.Services.Configure<AwsSettings>(builder.Configuration.GetSection("Aws"));
    
    var awsOptions = builder.Configuration.GetAWSOptions();
    if (!string.IsNullOrEmpty(builder.Configuration["Aws:ServiceUrl"]))
    {
        awsOptions.Credentials = new Amazon.Runtime.BasicAWSCredentials("local", "local");
    }
    
    builder.Services.AddDefaultAWSOptions(awsOptions);
    builder.Services.AddAWSService<Amazon.SimpleNotificationService.IAmazonSimpleNotificationService>();
    builder.Services.AddAWSService<Amazon.SQS.IAmazonSQS>();
    builder.Services.AddScoped<IMessageBus, AwsMessageBus>();
    
    // Registre seus background services que publicam/consomem mensagens
    // builder.Services.AddHostedService<OutboxWorker>();
    // builder.Services.AddHostedService<InboxConsumer>();
}
else
{
    builder.Services.AddScoped<IMessageBus, DummyMessageBus>();
}

var app = builder.Build();

// ... resto da aplicação ...
```

### 6. appsettings.Development.json
```json
{
  "EnableAwsServices": false,
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Aws": {
    "Region": "us-east-1",
    "TopicArn": "arn:aws:sns:us-east-1:000000000000:processador-diagramas-processingservice-hml-topic",
    "QueueUrl": "http://localhost:4566/000000000000/processador-diagramas-processingservice-hml-queue",
    "ServiceUrl": "http://localhost:4566"
  }
}
```

### 7. appsettings.json (Produção)
```json
{
  "EnableAwsServices": true,
  "Aws": {
    "Region": "us-east-1",
    "TopicArn": "arn:aws:sns:us-east-1:767398027345:processador-diagramas-processingservice-hml-topic",
    "QueueUrl": "https://sqs.us-east-1.amazonaws.com/767398027345/processador-diagramas-processingservice-hml-queue"
  }
}
```

### 8. scripts/init-aws-resources.sh
```bash
#!/bin/bash
set -e

echo "=== Provisionando Recursos AWS para Processador Diagramas Processing Service ==="

REGION="us-east-1"
TOPIC_NAME="processador-diagramas-processingservice-hml-topic"
QUEUE_NAME="processador-diagramas-processingservice-hml-queue"

# Verificar se AWS CLI está disponível
if ! command -v aws &> /dev/null; then
    echo "❌ AWS CLI não está instalado"
    exit 1
fi

echo "📝 Criando tópico SNS: $TOPIC_NAME"
TOPIC_ARN=$(aws sns create-topic \
  --name $TOPIC_NAME \
  --region $REGION \
  --query 'TopicArn' \
  --output text 2>/dev/null || echo "")

if [ -z "$TOPIC_ARN" ]; then
    TOPIC_ARN=$(aws sns list-topics --region $REGION --query "Topics[?contains(TopicArn, '$TOPIC_NAME')].TopicArn" --output text)
fi

echo "✅ Tópico SNS criado: $TOPIC_ARN"

echo "📝 Criando fila SQS: $QUEUE_NAME"
QUEUE_URL=$(aws sqs create-queue \
  --queue-name $QUEUE_NAME \
  --region $REGION \
  --query 'QueueUrl' \
  --output text 2>/dev/null || echo "")

if [ -z "$QUEUE_URL" ]; then
    QUEUE_URL=$(aws sqs list-queues --region $REGION --queue-name-prefix $QUEUE_NAME --query 'QueueUrls[0]' --output text)
fi

echo "✅ Fila SQS criada: $QUEUE_URL"

echo "📝 Obtendo ARN da fila"
QUEUE_ARN=$(aws sqs get-queue-attributes \
  --queue-url $QUEUE_URL \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

echo "✅ ARN da fila: $QUEUE_ARN"

echo "📝 Inscrevendo fila ao tópico SNS"
SUBSCRIPTION_ARN=$(aws sns subscribe \
  --topic-arn $TOPIC_ARN \
  --protocol sqs \
  --notification-endpoint $QUEUE_ARN \
  --region $REGION \
  --query 'SubscriptionArn' \
  --output text)

echo "✅ Fila inscrita ao tópico: $SUBSCRIPTION_ARN"

echo "📝 Definindo política de acesso da fila"
aws sqs set-queue-attributes \
  --queue-url $QUEUE_URL \
  --attributes Key=Policy,Value="{
    \"Version\": \"2012-10-17\",
    \"Statement\": [
      {
        \"Effect\": \"Allow\",
        \"Principal\": {
          \"Service\": \"sns.amazonaws.com\"
        },
        \"Action\": \"sqs:SendMessage\",
        \"Resource\": \"$QUEUE_ARN\",
        \"Condition\": {
          \"ArnEquals\": {
            \"aws:SourceArn\": \"$TOPIC_ARN\"
          }
        }
      }
    ]
  }" \
  --region $REGION

echo "✅ Política de acesso configurada"

echo ""
echo "==========================================="
echo "✅ RECURSOS PROVISIONADOS COM SUCESSO"
echo "==========================================="
echo "Tópico SNS: $TOPIC_ARN"
echo "Fila SQS: $QUEUE_URL"
echo "Subscription: $SUBSCRIPTION_ARN"
echo ""
echo "Use estas informações no appsettings.json:"
echo "  TopicArn: $TOPIC_ARN"
echo "  QueueUrl: $QUEUE_URL"
echo ""
```

---

## PASSOS DE IMPLEMENTAÇÃO

1. **Criar a interface IMessageBus** em `Domain/Interfaces/IMessageBus.cs`

2. **Implementar AwsMessageBus** em `Infrastructure/Messaging/AwsMessageBus.cs`
   - Seguir exatamente o padrão acima
   - NUNCA chamar: sns:Subscribe, sns:CreateTopic, sns:SetSubscriptionAttributes, sqs:CreateQueue, sqs:SetQueueAttributes, sqs:PurgeQueue

3. **Implementar DummyMessageBus** em `Infrastructure/Messaging/DummyMessageBus.cs`

4. **Registrar em Program.cs** com lógica condicional
   - EnableAwsServices = false em Development
   - EnableAwsServices = true em Production

5. **Refatorar appsettings.Development.json**
   - Adicionar `"EnableAwsServices": false`

6. **Criar scripts/init-aws-resources.sh**
   - Executar uma única vez com credenciais administrativas
   - Provisiona SNS topic, SQS queue, subscription

7. **Procurar e REMOVER** qualquer chamada a:
   - `sns.Subscribe(...)`
   - `sns.CreateTopic(...)`
   - `sqs.CreateQueue(...)`
   - `sqs.PurgeQueue(...)`

---

## POLÍTICA AWS RECOMENDADA (Solicitar ao DevOps)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "sns:Publish",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage"
      ],
      "Resource": [
        "arn:aws:sns:us-east-1:767398027345:processador-diagramas-processingservice-hml-topic",
        "arn:aws:sqs:us-east-1:767398027345:processador-diagramas-processingservice-hml-queue"
      ]
    }
  ]
}
```

Operações de infraestrutura (Subscribe, CreateTopic, SetSubscriptionAttributes, CreateQueue, SetQueueAttributes, PurgeQueue) NÃO serão necessárias em tempo de runtime com essa arquitetura.
