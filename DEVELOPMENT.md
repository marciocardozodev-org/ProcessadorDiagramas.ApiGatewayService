# Guia de Desenvolvimento Local com Minikube

## Por que Minikube?

- **Sem custo:** Roda no seu laptop, não usa créditos AWS Academy
- **Iteração rápida:** Testar múltiplas vezes sem esperar 15 min de cluster creation
- **Idêntico:** Usa Kubernetes localmente, mesma sintaxe/manifests da AWS EKS
- **Economia:** AWS Academy é para validação final, não para dev diário

---

## Fluxo recomendado

### 1. Setup inicial (fazer uma vez)

```bash
./scripts/setup-minikube.sh
```

Isso instala e inicia minikube. Levará alguns minutos na primeira vez.

### 2. Loop de desenvolvimento (repetir para cada teste)

```bash
# a) Build + testes
dotnet build
dotnet test

# b) Teste funcional completo local
./scripts/test-minikube-postgres.sh
```

O script completo valida:

- PostgreSQL local no cluster
- migrations via job
- health check
- upload autenticado
- simulacao do retorno do servico interno
- consulta de status final
- consulta de relatorio mock local

Se quiser apenas um smoke test do pod e health endpoint:

```bash
# c) Build imagem Docker
docker build -t processador-diagramas:local .

# d) Load no minikube
minikube image load processador-diagramas:local

# e) Smoke test
./scripts/test-minikube-local.sh
```

Para chamadas manuais aos endpoints protegidos em Development, use:

```bash
X-Api-Key: dev-client-key
```

Para o endpoint interno de simulacao, use:

```bash
X-Api-Key: dev-internal-key
```

Debug opcional:
- `kubectl get pods -n app`
- `kubectl logs -f deployment/processador-diagramas-apigatewayservice -n app`
- `kubectl port-forward svc/processador-diagramas-apigatewayservice 8080:80 -n app`
- `curl http://localhost:8080/health`

### 3. Cleanup (ao terminar sessão de testes)

```bash
./scripts/cleanup-minikube.sh
```

Isso remove o cluster e libera ~2GB RAM.

---

## Próxima etapa: AWS EKS

Depois que validar localmente:

1. Fazer merge `develop → homolog` no GitHub
2. Pipeline automaticamente:
   - Build imagem Docker
   - Cria cluster EKS em `us-east-1`
   - Faz deploy com as migrations
3. Validar em `homolog`  
4. Se OK, merge `homolog → master` para `production`

---

## Dicas

- **Imagem customizada no minikube:** Use `imagePullPolicy: Never` em `deployment.yaml` para testar localmente
- **Deletar/recriar cluster:** `./scripts/cleanup-minikube.sh && ./scripts/setup-minikube.sh`
- **Monitorar recursos:** `watch kubectl get all`
- **Logs em tempo real:** `kubectl logs -f <pod-name>`
- **Shell no pod:** `kubectl exec -it <pod-name> -- /bin/bash`

---

## Troubleshooting

**Minikube não inicia?**
```bash
minikube delete --all
minikube start --driver=docker --memory=2048
```

**Imagem não aparece no minikube?**
```bash
minikube image list  # Ver imagens disponíveis
docker tag seu-nome:tag minikube-tag:tag  # Marcar antes de load
minikube image load seu-nome:tag
```

**Cluster lento?**
```bash
minikube delete --all  # Recomeçar limpo
```

---

## Credenciais AWS Academy

- **Para Minikube local:** ❌ Não precisa
- **Para EKS após merge:** ✅ Precisa, secretamente no GitHub
  - AWS_ACCESS_KEY_ID
  - AWS_SECRET_ACCESS_KEY
  - AWS_SESSION_TOKEN (renovar a cada 4h)

