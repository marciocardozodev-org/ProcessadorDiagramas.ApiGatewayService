# Recomendação: Resolução de Erros de Permissão SNS/SQS

## Contexto do Problema

O outro microserviço recebe erros de autorização AWS com a política `voc-cancel-cred` negando:
- ❌ **SNS:Publish** (crítico - deveria estar permitido)
- ❌ **SNS:SetSubscriptionAttributes** (correto estar negado)
- ❌ **SNS:Subscribe** (correto estar negado)
- ❌ **sqs:purgequeue** (correto estar negado)

---

## ✅ Como Este Microserviço (ProcessadorDiagramas.APIGatewayService) Evita o Problema

### 1. **Separação: Infraestrutura vs. Runtime**

**Em `scripts/init-localstack.sh` (PROVISÃO - chamadas administrativas):**
```bash
# ✅ Criar tópico SNS (uma única vez)
aws sns create-topic --name diagram-requests

# ✅ Criar fila SQS (uma única vez)
aws sqs create-queue --queue-name diagram-events

# ✅ INSCREVER tópico em fila (uma única vez - requer SNS:Subscribe)
aws sns subscribe \
  --topic-arn arn:aws:sns:us-east-1:000000000000:diagram-requests \
  --protocol sqs \
  --notification-endpoint arn:aws:sqs:us-east-1:000000000000:diagram-events

# ✅ Definir política de fila (uma única vez - requer SNS:SetSubscriptionAttributes)
aws sqs set-queue-attributes \
  --queue-url http://localhost:4566/000000000000/diagram-events \
  --attributes Key=Policy,Value='{"Statement":[...]}'
```

**Em `Infrastructure/Messaging/AwsMessageBus.cs` (RUNTIME - apenas leitura/publish):**
```csharp
// ✅ Runtime: APENAS Publish (SNS:Publish)
public async Task PublishAsync(string eventType, string payload)
{
    await _sns.PublishAsync(new PublishRequest
    {
        TopicArn = _settings.TopicArn,
        Message = payload,
        MessageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            ["eventType"] = new MessageAttributeValue { StringValue = eventType }
        }
    });
}

// ✅ Runtime: APENAS ReceiveMessage (SQS:ReceiveMessage)
public async Task SubscribeAsync(Func<BusMessage, Task> handler, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _settings.QueueUrl,
            WaitTimeSeconds = 20, // Long polling
            MaxNumberOfMessages = 1
        });

        foreach (var message in response.Messages)
        {
            var payload = ExtractPayload(message.Body);
            await handler(new BusMessage { Payload = payload });
            
            // ✅ Runtime: DeleteMessage (SQS:DeleteMessage)
            await _sqs.DeleteMessageAsync(_settings.QueueUrl, message.ReceiptHandle);
        }
    }
}

// ❌ NUNCA CHAMADO: SNS:Subscribe, SNS:SetSubscriptionAttributes, SQS:PurgeQueue
```

---

## 🎯 Permissões AWS Necessárias (Mínimas)

### Política para Aplicação em Runtime:
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

### Política para Provisão de Infraestrutura (Separada, contexto administrativo):
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "sns:CreateTopic",
        "sns:Subscribe",
        "sns:SetSubscriptionAttributes",
        "sqs:CreateQueue",
        "sqs:SetQueueAttributes"
      ],
      "Resource": "*"
    }
  ]
}
```

---

## 🔧 Solução: Refatorar o Outro Microserviço

### Passo 1: Verificar o Código Runtime

**❌ PROCURAR POR (e REMOVER de tempo de runtime):**
```csharp
// Nunca fazer isso em tempo de execução:
_sns.Subscribe(...);           // ❌ SNS:Subscribe
_sns.SetSubscriptionAttributes(...);  // ❌ SNS:SetSubscriptionAttributes
_sqs.PurgeQueue(...);          // ❌ SQS:PurgeQueue
_sns.CreateTopic(...);         // ❌ SNS:CreateTopic
_sqs.CreateQueue(...);         // ❌ SQS:CreateQueue
```

**✅ PROCURAR POR (e MANTER em tempo de runtime):**
```csharp
_sns.PublishAsync(...)         // ✅ SNS:Publish
_sqs.ReceiveMessageAsync(...)  // ✅ SQS:ReceiveMessage
_sqs.DeleteMessageAsync(...)   // ✅ SQS:DeleteMessage
```

### Passo 2: Implementar Abstração de Message Bus

Copiar o padrão de `IMessageBus`:

```csharp
public interface IMessageBus
{
    Task PublishAsync(string eventType, string payload);
    Task SubscribeAsync(Func<BusMessage, Task> handler, CancellationToken cancellationToken);
}
```

Implementações:
- `AwsMessageBus` → Real AWS (produção)
- `LocalStackMessageBus` → LocalStack (testes E2E)
- `DummyMessageBus` → Nenhuma operação (testes unitários)

### Passo 3: Registrar Condicionalmente em `Program.cs`

```csharp
var enableAwsServices = !builder.Environment.IsDevelopment() || 
    builder.Configuration.GetValue<bool>("EnableAwsServices", false);

if (enableAwsServices)
{
    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddAWSService<IAmazonSimpleNotificationService>();
    builder.Services.AddAWSService<IAmazonSQS>();
    builder.Services.AddScoped<IMessageBus, AwsMessageBus>();
    builder.Services.AddHostedService<OutboxWorker>();  // Publica para SNS
    builder.Services.AddHostedService<InboxConsumer>();  // Consome de SQS
}
else
{
    builder.Services.AddScoped<IMessageBus, DummyMessageBus>();
}
```

### Passo 4: Mover Provisão de Infraestrutura para Script

Criar `scripts/init-aws-resources.sh` (para rodar com credenciais administrativas, UMA ÚNICA VEZ):

```bash
#!/bin/bash
# Executar com: AWS_PROFILE=infra-admin ./scripts/init-aws-resources.sh

REGION="us-east-1"
TOPIC_NAME="processador-diagramas-procesingservice-hml-topic"
QUEUE_NAME="processador-diagramas-processingservice-hml-queue"

# Criar tópico
TOPIC_ARN=$(aws sns create-topic \
  --name $TOPIC_NAME \
  --region $REGION \
  --query 'TopicArn' \
  --output text)

# Criar fila
QUEUE_URL=$(aws sqs create-queue \
  --queue-name $QUEUE_NAME \
  --region $REGION \
  --query 'QueueUrl' \
  --output text)

QUEUE_ARN=$(aws sqs get-queue-attributes \
  --queue-url $QUEUE_URL \
  --attribute-names QueueArn \
  --region $REGION \
  --query 'Attributes.QueueArn' \
  --output text)

# Inscrever fila ao tópico
aws sns subscribe \
  --topic-arn $TOPIC_ARN \
  --protocol sqs \
  --notification-endpoint $QUEUE_ARN \
  --region $REGION

# Definir política de acesso da fila
aws sqs set-queue-attributes \
  --queue-url $QUEUE_URL \
  --attributes Key=Policy,Value='{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Principal": {"Service": "sns.amazonaws.com"},
      "Action": "sqs:SendMessage",
      "Resource": "'$QUEUE_ARN'",
      "Condition": {
        "ArnEquals": {"aws:SourceArn": "'$TOPIC_ARN'"}
      }
    }]
  }' \
  --region $REGION

echo "✅ Recursos provisionados:"
echo "   Tópico: $TOPIC_ARN"
echo "   Fila: $QUEUE_URL"
```

---

## 🚀 Estratégia de Implementação Imediata

### Opção A: Refatoração Completa (Ideal, 3-5 dias)
1. Implementar IMessageBus com AwsMessageBus/DummyMessageBus
2. Mover todas as provisões para script de infraestrutura
3. Testes unitários com DummyMessageBus
4. Testes E2E com LocalStack (sem credenciais AWS reais)

### Opção B: Workaround Imediato (1 dia)
1. Desativar AWS services em desenvolvimento:
   ```json
   // appsettings.Development.json
   {
     "EnableAwsServices": false,  // Usar DummyMessageBus
     "UseLocalStack": true
   }
   ```
2. Testes E2E rodam com LocalStack (sem Deny da política)
3. Produção mantém permissões mínimas (apenas SNS:Publish, SQS:Receive/Delete)

### Opção C: Correção de Política (1 dia, requer DevOps)
1. **Corrigir a política `voc-cancel-cred`** para permitir `SNS:Publish` (essencial!)
2. **Manter negados**: SNS:Subscribe, SNS:SetSubscriptionAttributes, SQS:PurgeQueue
3. Refatorar código para não chamar operações negadas

---

## 📋 Checklist de Diagnóstico

Fazer essas perguntas ao outro time:

- [ ] **O código tenta criar SNS Topic em tempo de execução?** → ❌ Remover
- [ ] **O código tenta chamar `sns.Subscribe()` dentro da app?** → ❌ Remover, provisionar em script
- [ ] **O código tenta chamar `sqs.PurgeQueue()`?** → ❌ Remover, não é necessário
- [ ] **SNS:Publish está realmente sendo negado?** → ⚠️ Sim, é um problema de política (deve estar Allow)
- [ ] **O código só chama SNS:Publish e SQS:Receive/Delete em runtime?** → ✅ Correto, segue este padrão
- [ ] **Existe abstração de Message Bus (IMessageBus)?** → Se não, implementar

---

## 📊 Comparação: Outro Serviço vs. ProcessadorDiagramas

| Operação | Deve fazer em Runtime? | Este Microserviço | Recomendação |
|----------|------------------------|-------------------|--------------|
| SNS:Publish | ✅ Sim | ✅ Sim | **Permitir na política** |
| SNS:Subscribe | ❌ Não | ❌ Não (init script) | **Mover para infraestrutura** |
| SNS:SetSubscriptionAttributes | ❌ Não | ❌ Não (init script) | **Mover para infraestrutura** |
| SNS:CreateTopic | ❌ Não | ❌ Não (init script) | **Mover para infraestrutura** |
| SQS:ReceiveMessage | ✅ Sim | ✅ Sim | **Permitir na política** |
| SQS:DeleteMessage | ✅ Sim | ✅ Sim | **Permitir na política** |
| SQS:CreateQueue | ❌ Não | ❌ Não (init script) | **Mover para infraestrutura** |
| SQS:SetQueueAttributes | ❌ Não | ❌ Não (init script) | **Mover para infraestrutura** |
| SQS:PurgeQueue | ❌ Não | ❌ Não | **Nunca chamar** |

---

## 🔗 Padrão de Código Relevante Neste Repo

**Arquivo de referência:** `Infrastructure/Messaging/AwsMessageBus.cs`
- Mostra como fazer SNS:Publish com mensagens customizadas
- Mostra como fazer SQS long-polling com ReceiveMessage

**Arquivo de referência:** `scripts/init-localstack.sh`
- Mostra como provisionar SNS topic + SQS queue + subscription (fora da app)

**Arquivo de referência:** `Program.cs` (linhas 51-66)
- Mostra como registrar AwsMessageBus/DummyMessageBus condicionalmente

**Arquivo de referência:** `appsettings.Development.json`
- Mostra como apontar para LocalStack em dev (sem AWS credentials)

---

## 🎬 Próximos Passos Recomendados

1. **Hoje**: Compartilhar esse documento com o outro time
2. **Amanhã**: Revisar código do outro microserviço procurando por sns:Subscribe, sns:CreateTopic, sqs:PurgeQueue
3. **Próximos 3 dias**: Refatoração para abstrair message bus
4. **Longo prazo**: Garantir que testes E2E usem LocalStack (sem credentials AWS reais)

---

**Responsável pela análise:** Análise do padrão em ProcessadorDiagramas.APIGatewayService
**Data:** 2026-05-09
**Conclusão:** O padrão deste repo evita 100% de erros de permissão separando infraestrutura de runtime e usando abstração de message bus.
