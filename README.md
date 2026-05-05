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
dotnet run
```

```bash
# 2. Simular retorno do servico interno
curl -X POST "http://localhost:5000/internal/testing/diagram-processed" \
# 3. Consultar status e relatorio
curl -H "X-Api-Key: dev-client-key" "http://localhost:5000/api/diagrams/SEU_ID"
curl -H "X-Api-Key: dev-client-key" "http://localhost:5000/api/diagrams/SEU_ID/report"
```

---
# Subir toda a infraestrutura (PostgreSQL, LocalStack, inicializar SNS/SQS)
ENABLE_AWS_SERVICES=true docker compose up --build

# Ou em uma sessao separada, apenas a infra mais leve
docker compose up postgres localstack -d
bash scripts/init-localstack.sh
```

### 2. Validar que SNS/SQS estao funcionando
# Testa conectividade e cria topico/fila se nao existirem
bash scripts/test-sqs-sns-local.sh
Esperado: **All tests passed!**
### 3. Testar end-to-end: enviar diagrama e monitorar fila

```bash
# Inicia API em container (ou via 'dotnet run' em outro terminal)
# Garanta que appsettings.Development.json tem EnableAwsServices=true

# Envia requisicao para criar diagrama e monitora fila SQS
2. API persiste em banco + publica evento no SNS (via padrão Outbox)
3. SNS roteia para fila SQS
4. InboxConsumer consome mensagem de SQS
5. Handlers processam o evento (ex: DiagramProcessedEventHandler)

### 4. Comandos cURL úteis para testar manualmente

Listar mensagens na fila:
```bash
aws sqs receive-message \
  --queue-url "http://localhost:4566/000000000000/diagram-events" \
  --endpoint-url "http://localhost:4566" \
  --region us-east-1
```

Publicar mensagem diretamente no SNS:
```bash
aws sns publish \
  --topic-arn "arn:aws:sns:us-east-1:000000000000:diagram-requests" \
  --message '{"eventType":"DiagramProcessed","payload":{"diagramId":"test-123"}}' \
  --endpoint-url "http://localhost:4566" \
  --region us-east-1
```

Purgar fila (deletar todas as mensagens):
```bash
aws sqs purge-queue \
  --queue-url "http://localhost:4566/000000000000/diagram-events" \
  --endpoint-url "http://localhost:4566" \
  --region us-east-1
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
- AWS__TOPICARN
- REPORTSERVICE__BASEURL
- AUTH__CLIENTAPIKEY
- AUTH__INTERNALAPIKEY
- RDS_MASTER_USERNAME
- RDS_MASTER_PASSWORD

Observacao sobre `CONNECTIONSTRINGS__DEFAULTCONNECTION`:
- Agora a pipeline prioriza criar/iniciar o RDS automaticamente e montar a connection string com endpoint dinamico.
- `CONNECTIONSTRINGS__DEFAULTCONNECTION` pode ser mantida como fallback, mas deixa de ser obrigatoria quando o fluxo automatico de RDS estiver configurado.

Variables recomendadas no GitHub (Repository Variables):
- AWS_REGION
- EKS_CLUSTER_NAME (recomendado: processador-diagramas-shared-eks)
- RDS_DB_INSTANCE_IDENTIFIER
- RDS_DB_SUBNET_GROUP_NAME
- RDS_DB_VPC_SECURITY_GROUP_IDS
- RDS_DB_NAME (opcional, default: processador_diagramas)
- RDS_DB_ENGINE_VERSION (opcional; se vazio, usa automaticamente a versão default disponível da AWS)
- RDS_DB_INSTANCE_CLASS (opcional, default: db.t3.micro)
- RDS_DB_ALLOCATED_STORAGE (opcional, default: 20)
- RDS_DB_STORAGE_TYPE (opcional, default: gp3)
- RDS_DB_PORT (opcional, default: 5432)
- RDS_DB_BACKUP_RETENTION_DAYS (opcional, default: 1)
- RDS_DB_MULTI_AZ (opcional, default: false)
- RDS_DB_AUTO_MINOR_VERSION_UPGRADE (opcional, default: false)

Compatibilidade:
- A pipeline usa AWS_REGION e EKS_CLUSTER_NAME via Variables e faz fallback para Secrets com os mesmos nomes.
- Tambem aceita aliases para compatibilidade entre repositorios: AWS_DEFAULT_REGION e AWS_EKS_CLUSTER_NAME (alem de CLUSTER_NAME para o nome do cluster).

Observacao:
- Se estiver usando credenciais temporarias do AWS Academy, preencha tambem AWS_SESSION_TOKEN nos secrets do GitHub. Essas credenciais expiram, entao e preciso atualizar os tres valores AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_SESSION_TOKEN quando forem renovados.

Promocao esperada:
- pull request para develop: valida build e testes
- merge em homolog: publica imagem e faz deploy no namespace homolog
- merge em master: publica imagem e faz deploy no namespace production

## EKS compartilhado economico (AWS Academy)

Objetivo: usar um cluster unico para os microservicos por ambiente e reduzir custo operacional.

Nome recomendado do cluster para este projeto:
- processador-diagramas-shared-eks

Padrao de deploy recomendado:
- 1 cluster EKS compartilhado por ambiente (ex.: homolog/producao)

Como criar/gerenciar com este repositorio:
- Script local: `scripts/eks-manage.sh`
- Workflow manual: `Manage EKS Cluster` (`.github/workflows/eks-manage.yml`)

Acoes disponiveis:
- `ensure`: cria cluster e nodegroup se nao existirem
- `status`: mostra status do cluster e escala do nodegroup
- `pause`: escala nodegroup para 0 (economia de EC2)
- `resume`: restaura escala minima do nodegroup
- `delete`: remove nodegroup e cluster (maior economia)

Estrategia de economia de credito (AWS Academy):
- EKS nao possui stop/start nativo como RDS
- `pause` economiza custo de EC2 (workers), mas o control plane do EKS continua cobrando
- para economia maxima, use `delete` ao fim da sessao e `ensure` ao retomar
- para economia com retomada rapida no mesmo dia, use `pause`/`resume`

Rotina diaria pronta (scripts auxiliares):
- Fim do dia (economia maxima): `scripts/day-end-max-economy.sh`
  - remove EKS (cluster + nodegroup)
  - para o RDS
- Inicio do dia (restaurar ambiente): `scripts/day-start-restore.sh`
  - recria/garante EKS
  - inicia/garante RDS
  - atualiza kubeconfig local

Exemplo de uso:
```bash
# Economia maxima ao encerrar o trabalho
AWS_REGION=us-east-1 ./scripts/day-end-max-economy.sh

# Restaurar no proximo dia
AWS_REGION=us-east-1 ./scripts/day-start-restore.sh
```

## RDS PostgreSQL economico (AWS Academy)

Objetivo: criar o banco com custo minimo e ligar/desligar por demanda durante os estudos.

Configuracao recomendada de menor custo para laboratorio:
- Engine: PostgreSQL (versão default disponível no momento da criação, salvo se você fixar `RDS_DB_ENGINE_VERSION`)
- Classe: `db.t3.micro` (ou `db.t4g.micro` se disponivel na conta)
- Storage: `gp3` com `20 GiB`
- Multi-AZ: `false`
- Public access: `false` (preferencial, para trafego interno no VPC)
- Backup retention: `1` dia
- Deletion protection: `false` (laboratorio)
- Performance Insights: `false`

Fluxo na pipeline:
1. Em push para `homolog` ou `master`, a pipeline executa `scripts/rds-manage.sh ensure`.
2. Se o RDS nao existir, ele e criado.
3. Se estiver parado, ele e iniciado.
4. Endpoint/porta retornam para a pipeline, que monta a connection string dinamica.
5. Migrations rodam e depois o deployment da API e aplicado.

Como desligar para economizar creditos:
- Use o workflow manual `Manage RDS PostgreSQL` com a acao `stop`.
- Ao retomar estudos, execute novamente com `start` (ou `ensure`).

Importante:
- Instancias RDS paradas podem ser religadas automaticamente pela AWS apos alguns dias. Antes de estudar, rode `start`/`ensure`.
- Para economia maxima, finalize sempre com `stop` ao terminar a sessao de estudos.

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
