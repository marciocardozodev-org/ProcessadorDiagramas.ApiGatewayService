# ProcessadorDiagramas.APIGatewayService

API Gateway para o sistema de processamento de diagramas.
Recebe requisicoes de clientes, persiste no PostgreSQL, publica eventos no SNS via padrao Outbox
e consome o resultado via padrao Inbox com deduplicacao.

---

## Requisitos

- .NET 8 SDK
- Docker + Docker Compose
- dotnet-ef (para migrations)

---

## Variaveis de ambiente

| Variavel                              | Descricao                                  | Exemplo                                              |
|---------------------------------------|--------------------------------------------|------------------------------------------------------|
| ConnectionStrings__DefaultConnection  | Connection string do PostgreSQL             | Host=localhost;Database=pd;Username=postgres;...     |
| Aws__Region                           | Regiao AWS                                  | us-east-1                                            |
| Aws__TopicArn                         | ARN do topico SNS de saida                 | arn:aws:sns:us-east-1:000000000000:diagram-requests  |
| Aws__QueueUrl                         | URL da fila SQS de entrada                 | http://localhost:4566/000000000000/diagram-events    |
| Aws__ServiceURL                       | URL local do LocalStack (apenas dev)       | http://localhost:4566                                |
| ReportService__BaseUrl                | URL base do microservico de relatorios     | http://localhost:8081                                |
| ReportService__GetReportPathTemplate  | Template da rota de consulta de relatorio  | /api/reports/{analysisId}                            |
| ReportService__UseMock                | Usa relatorio mock local em desenvolvimento | true                                                 |
| Auth__HeaderName                      | Nome do header da API key                   | X-Api-Key                                            |
| Auth__ClientApiKey                    | API key para endpoints publicos             | dev-client-key                                       |
| Auth__InternalApiKey                  | API key para simulacoes internas            | dev-internal-key                                     |
| UploadStorage__RootPath               | Pasta local temporaria para uploads        | /tmp/uploads                                         |
| ASPNETCORE_ENVIRONMENT                | Ambiente da aplicacao                       | Development                                          |

---

## Rodar localmente com Docker Compose

```bash
# Subir PostgreSQL e a API
docker compose up --build

# Apenas a infraestrutura sem a API
docker compose up postgres

# Executar migrations a partir do host
dotnet tool install --global dotnet-ef
export PATH="$PATH:$HOME/.dotnet/tools"
cd src/ProcessadorDiagramas.APIGatewayService
dotnet ef database update
```

Depois de subir, acesse:
- **Swagger UI:** http://localhost:5000/swagger
- **Health check:** http://localhost:5000/health

Autenticacao local padrao:
- Header: X-Api-Key
- Chave de cliente: dev-client-key
- Chave interna de simulacao: dev-internal-key

---

## Rodar localmente sem Docker

```bash
# 1. Suba apenas a infra
docker compose up postgres -d

# 2. Configure as variaveis (ou use appsettings.Development.json)
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=processador_diagramas_dev;Username=postgres;Password=postgres"

# 3. Aplique as migrations
cd src/ProcessadorDiagramas.APIGatewayService
dotnet ef database update

# 4. Execute a API
dotnet run
```

Para concluir o fluxo local sem depender do servico real de processamento, use a simulacao interna em Development:

```bash
# 1. Criar solicitacao
curl -X POST "http://localhost:5000/api/diagrams" \
  -H "X-Api-Key: dev-client-key" \
  -F "file=@./diagram.png;type=image/png"

# 2. Simular retorno do servico interno
curl -X POST "http://localhost:5000/internal/testing/diagram-processed" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: dev-internal-key" \
  -d '{"diagramRequestId":"SEU_ID","isSuccess":true,"resultUrl":"https://reports.local/SEU_ID"}'

# 3. Consultar status e relatorio
curl -H "X-Api-Key: dev-client-key" "http://localhost:5000/api/diagrams/SEU_ID"
curl -H "X-Api-Key: dev-client-key" "http://localhost:5000/api/diagrams/SEU_ID/report"
```

---

## Rodar os testes

```bash
dotnet test
```

Saida esperada: **33 testes passando, 0 falhas.**

---

## CI/CD e deploy

Este repositorio segue o mesmo mecanismo pratico do OficinaCardozo.OSService:
- GitHub Actions acionado em pull requests e pushes para develop, homolog e master
- build e testes em todas as PRs do fluxo
- build e push de imagem Docker quando houver merge em homolog ou master
- deploy em Kubernetes/EKS apos o merge, com execucao das migrations antes da atualizacao da API
- banco de dados dedicado para este servico, consumido por secret no pipeline

Arquivos de deploy:
- .github/workflows/ci-cd.yml
- deploy/k8s/deployment.yaml
- deploy/k8s/service.yaml
- deploy/k8s/create-db-job.yaml
- infra/terraform/README.md

Secrets esperadas no GitHub:
- DOCKERHUB_USERNAME
- DOCKERHUB_TOKEN
- AWS_ACCESS_KEY_ID
- AWS_SECRET_ACCESS_KEY
- AWS_SESSION_TOKEN
- CONNECTIONSTRINGS__DEFAULTCONNECTION
- AWS__TOPICARN
- AWS__QUEUEURL
- REPORTSERVICE__BASEURL

Variables recomendadas no GitHub (Repository Variables):
- AWS_REGION
- EKS_CLUSTER_NAME

Compatibilidade:
- A pipeline usa AWS_REGION e EKS_CLUSTER_NAME via Variables e faz fallback para Secrets com os mesmos nomes.
- Tambem aceita aliases para compatibilidade entre repositorios: AWS_DEFAULT_REGION e AWS_EKS_CLUSTER_NAME (alem de CLUSTER_NAME para o nome do cluster).

Observacao:
- Se estiver usando credenciais temporarias do AWS Academy, preencha tambem AWS_SESSION_TOKEN nos secrets do GitHub. Essas credenciais expiram, entao e preciso atualizar os tres valores AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_SESSION_TOKEN quando forem renovados.

Promocao esperada:
- pull request para develop: valida build e testes
- merge em homolog: publica imagem e faz deploy no namespace homolog
- merge em master: publica imagem e faz deploy no namespace production

---

## Endpoints principais

### POST /api/diagrams
Cria uma nova requisicao de processamento de diagrama via upload multipart.

Regras:
- Campo obrigatorio: file
- Campo opcional: name
- Campo opcional: description
- Tipos aceitos: image/* e application/pdf
- Tamanho maximo: 10MB
- Storage temporario MVP: UploadStorage__RootPath, com padrao em /tmp/uploads
- Metadados persistidos na requisicao: fileName, fileSize, contentType e storagePath

```bash
curl -X POST "http://localhost:5000/api/diagrams" \
  -H "X-Api-Key: dev-client-key" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@./diagram.png;type=image/png" \
  -F "name=Arquitetura Checkout" \
  -F "description=Versao de referencia do fluxo de pagamento"
```

Resposta `201 Created`:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Received",
  "createdAt": "2026-03-14T20:00:00Z"
}
```

### GET /api/diagrams/{id}
Retorna o status atual da requisicao.

```bash
curl -H "X-Api-Key: dev-client-key" \
  "http://localhost:5000/api/diagrams/3fa85f64-5717-4562-b3fc-2c963f66afa6"
```

Resposta `200 OK`:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "format": "PlantUML",
  "status": "Analyzed",
  "receivedAt": "2026-03-14T20:00:00Z",
  "lastUpdatedAt": "2026-03-14T20:00:05Z",
  "reportUrl": "https://reports.local/analysis-123",
  "errorMessage": null
}
```

### POST /internal/testing/diagram-processed
Endpoint interno de apoio para desenvolvimento local. Disponivel apenas em `Development` e protegido por API key interna.

```bash
curl -X POST "http://localhost:5000/internal/testing/diagram-processed" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: dev-internal-key" \
  -d '{"diagramRequestId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","isSuccess":true,"resultUrl":"https://reports.local/analysis-123"}'
```

### GET /api/diagrams/{id}/report
Consulta o relatorio tecnico de uma analise finalizada.

Se a analise ainda nao estiver concluida, o endpoint retorna `409 Conflict`.

Resposta `200 OK`:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Analyzed",
  "reportUrl": "https://reports.local/analysis-123",
  "summary": "Architecture findings",
  "details": "Detected missing retry policy in integration flow.",
  "generatedAt": "2026-03-14T20:01:00Z"
}
```

---

## Fluxo de eventos

```
Cliente
  |-- POST /api/diagrams
  |-- DiagramRequest salvo no PostgreSQL (status: Received)
       |-- OutboxMessage registrado na mesma transacao
       |
OutboxWorker (background, a cada 10s)
  |-- Le OutboxMessages pendentes
  |-- Publica DiagramRequestCreatedEvent no SNS
  |-- Marca mensagem como processada
       |
[Servico downstream processa e publica DiagramProcessedEvent no SQS]
       |
InboxConsumer (background, long-polling SQS)
  |-- Recebe mensagem do SQS
  |-- Verifica deduplicacao via InboxMessage.MessageId
  |-- Aciona DiagramProcessedEventHandler
  |-- Atualiza DiagramRequest (status: Analyzed ou Error)
```

---

## Build e estrutura

```
src/ProcessadorDiagramas.APIGatewayService/
  API/           Controllers e DTOs
  Application/   Commands, Queries, Interfaces de aplicacao
  Contracts/     Eventos compartilhados (DiagramRequestCreatedEvent, DiagramProcessedEvent)
  Domain/        Entidades, Enums, Interfaces de dominio
  EventHandlers/ Handlers de eventos recebidos via Inbox
  Inbox/         InboxMessage (entidade) + InboxConsumer (background worker)
  Outbox/        OutboxMessage (entidade) + OutboxPublisher + OutboxWorker
  Infrastructure/
    Data/        AppDbContext + Repositories + Migrations
    Messaging/   AwsMessageBus + AwsSettings

tests/ProcessadorDiagramas.APIGatewayService.Tests/
  Domain/        Testes de regras de dominio
  Application/   Testes do command handler
  API/           Testes do controller e fluxo de upload multipart
  EventHandlers/ Testes do event handler
```
