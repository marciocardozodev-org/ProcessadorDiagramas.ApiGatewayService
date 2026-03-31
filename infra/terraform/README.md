# Infraestrutura - Banco de Dados

Esta pasta reserva a infraestrutura do banco dedicado do ProcessadorDiagramas.APIGatewayService.

O padrão adotado segue o mesmo mecanismo do repositório OficinaCardozo.OSService:
- o serviço possui banco próprio
- o provisionamento da infraestrutura fica desacoplado do código da aplicação
- o deploy da aplicação consome a connection string pronta via secret no pipeline

## Estratégia adotada neste repositório

No momento, o pipeline do serviço assume que o PostgreSQL já foi provisionado e que a secret abaixo estará disponível no GitHub Actions:

- CONNECTIONSTRINGS__DEFAULTCONNECTION

Com isso, o deploy executa as migrations do Entity Framework em um Job Kubernetes antes de atualizar o Deployment da API.

## Evolução esperada

Se o banco deste serviço também passar a ser provisionado automaticamente, mantenha o mesmo padrão do OSService:
- criar os arquivos Terraform nesta pasta
- separar o provisionamento do banco da aplicação
- reaproveitar outputs e secrets consumidos pelo workflow
