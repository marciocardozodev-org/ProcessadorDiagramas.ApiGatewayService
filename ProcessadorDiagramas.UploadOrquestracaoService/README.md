# ProcessadorDiagramas.UploadOrchestratorService

Microservico responsavel por receber uploads de diagramas e orquestrar o ciclo de analise com outros servicos.

## Arquitetura atual (Etapas 1 a 3)

Estrutura em camadas separadas:

- API: endpoints REST internos e bootstrap.
- Application: casos de uso e contratos de portas.
- Domain: entidades e regras de negocio.
- Infrastructure: persistencia, mensageria e storage.
- Tests: testes automatizados.

## Banco de dados proprio

Persistencia inicial com EF Core + PostgreSQL implementada em:

- tabela analysis_processes
- tabela analysis_process_status_history

Migration inicial gerada em:

- src/ProcessadorDiagramas.UploadOrchestratorService.Infrastructure/Data/Migrations

## Projetos

- src/ProcessadorDiagramas.UploadOrchestratorService.Api
- src/ProcessadorDiagramas.UploadOrchestratorService.Application
- src/ProcessadorDiagramas.UploadOrchestratorService.Domain
- src/ProcessadorDiagramas.UploadOrchestratorService.Infrastructure
- tests/ProcessadorDiagramas.UploadOrchestratorService.Tests

## Como executar

```bash
dotnet restore ProcessadorDiagramas.UploadOrchestratorService.sln
dotnet build ProcessadorDiagramas.UploadOrchestratorService.sln
dotnet test ProcessadorDiagramas.UploadOrchestratorService.sln
```

## Aplicar migrations

```bash
dotnet ef database update \
	--project src/ProcessadorDiagramas.UploadOrchestratorService.Infrastructure/ProcessadorDiagramas.UploadOrchestratorService.Infrastructure.csproj \
	--startup-project src/ProcessadorDiagramas.UploadOrchestratorService.Api/ProcessadorDiagramas.UploadOrchestratorService.Api.csproj
```
