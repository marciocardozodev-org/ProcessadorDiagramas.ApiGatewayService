#!/bin/bash

# Teste local completo no Minikube com PostgreSQL real + migrations + endpoint de negocio.

set -euo pipefail

PROFILE="processador-dev"
NAMESPACE="app"
IMAGE="processador-diagramas:dev-$(date +%s)"
MANIFEST="/tmp/processador-minikube-postgres.yaml"
PF_PID_FILE="/tmp/processador-pf-postgres.pid"

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "[ERROR] Comando obrigatório não encontrado: $cmd"
    exit 1
  fi
}

cleanup() {
  if [[ -f "$PF_PID_FILE" ]]; then
    kill "$(cat "$PF_PID_FILE")" >/dev/null 2>&1 || true
    rm -f "$PF_PID_FILE"
  fi
}

trap cleanup EXIT

require_cmd kubectl
require_cmd minikube
require_cmd curl
require_cmd dotnet
require_cmd docker
require_cmd grep

if ! minikube status --profile "$PROFILE" >/dev/null 2>&1; then
  echo "[ERROR] Profile do Minikube '$PROFILE' não está ativo. Rode ./scripts/setup-minikube.sh primeiro."
  exit 1
fi

echo "[INFO] Build .NET..."
dotnet build

echo "[INFO] Build imagem Docker..."
docker build -t "$IMAGE" -f Dockerfile .

echo "[INFO] Carregando imagem no Minikube..."
minikube image load "$IMAGE" --profile "$PROFILE"

cat > "$MANIFEST" <<'YAML'
apiVersion: v1
kind: Namespace
metadata:
  name: app
---
apiVersion: v1
kind: Secret
metadata:
  name: postgres-secrets
  namespace: app
type: Opaque
stringData:
  POSTGRES_USER: postgres
  POSTGRES_PASSWORD: postgres
  POSTGRES_DB: processador_diagramas
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres
  namespace: app
  labels:
    app: postgres
spec:
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:16-alpine
        ports:
        - containerPort: 5432
        envFrom:
        - secretRef:
            name: postgres-secrets
        readinessProbe:
          exec:
            command:
            - sh
            - -c
            - pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"
          initialDelaySeconds: 5
          periodSeconds: 5
        livenessProbe:
          exec:
            command:
            - sh
            - -c
            - pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"
          initialDelaySeconds: 15
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: app
spec:
  selector:
    app: postgres
  ports:
  - protocol: TCP
    port: 5432
    targetPort: 5432
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: processador-diagramas-apigatewayservice-config
  namespace: app
data:
  ASPNETCORE_ENVIRONMENT: Development
  EnableAwsServices: "false"
  ReportService__BaseUrl: "http://mock-report-service.local"
  ReportService__UseMock: "true"
  Auth__ClientApiKey: "dev-client-key"
  Auth__InternalApiKey: "dev-internal-key"
---
apiVersion: v1
kind: Secret
metadata:
  name: processador-diagramas-apigatewayservice-secrets
  namespace: app
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=postgres.app.svc.cluster.local;Port=5432;Database=processador_diagramas;Username=postgres;Password=postgres"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: processador-diagramas-apigatewayservice
  namespace: app
  labels:
    app: processador-diagramas-apigatewayservice
spec:
  replicas: 1
  selector:
    matchLabels:
      app: processador-diagramas-apigatewayservice
  template:
    metadata:
      labels:
        app: processador-diagramas-apigatewayservice
    spec:
      containers:
      - name: processador-diagramas-apigatewayservice
        image: __API_IMAGE__
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8080
        envFrom:
        - configMapRef:
            name: processador-diagramas-apigatewayservice-config
        - secretRef:
            name: processador-diagramas-apigatewayservice-secrets
        env:
        - name: ASPNETCORE_URLS
          value: http://+:8080
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 20
          periodSeconds: 10
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 20
---
apiVersion: v1
kind: Service
metadata:
  name: processador-diagramas-apigatewayservice
  namespace: app
spec:
  selector:
    app: processador-diagramas-apigatewayservice
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: ClusterIP
YAML

sed -i "s|__API_IMAGE__|$IMAGE|g" "$MANIFEST"

echo "[INFO] Aplicando infraestrutura local (namespace, postgres, config/secrets)..."
kubectl apply -f "$MANIFEST"

echo "[INFO] Aguardando PostgreSQL ficar pronto..."
kubectl wait --for=condition=ready pod -l app=postgres -n "$NAMESPACE" --timeout=180s

echo "[INFO] Executando migration job..."
kubectl delete job processador-diagramas-apigatewayservice-migrations-local -n "$NAMESPACE" --ignore-not-found
cat <<YAML | kubectl apply -f -
apiVersion: batch/v1
kind: Job
metadata:
  name: processador-diagramas-apigatewayservice-migrations-local
  namespace: $NAMESPACE
spec:
  backoffLimit: 1
  template:
    spec:
      restartPolicy: Never
      containers:
      - name: migrations
        image: $IMAGE
        imagePullPolicy: IfNotPresent
        envFrom:
        - secretRef:
            name: processador-diagramas-apigatewayservice-secrets
        command:
        - /bin/sh
        - -c
        - /app/efbundle --connection "\$ConnectionStrings__DefaultConnection"
YAML
kubectl wait --for=condition=complete job/processador-diagramas-apigatewayservice-migrations-local -n "$NAMESPACE" --timeout=240s

echo "[INFO] Aguardando API ficar disponível..."
kubectl rollout status deployment/processador-diagramas-apigatewayservice -n "$NAMESPACE" --timeout=240s
kubectl wait --for=condition=available deployment/processador-diagramas-apigatewayservice -n "$NAMESPACE" --timeout=240s

echo "[INFO] Iniciando port-forward para testes HTTP..."
kubectl port-forward -n "$NAMESPACE" svc/processador-diagramas-apigatewayservice 18080:80 >/tmp/processador-pf-postgres.log 2>&1 &
echo $! > "$PF_PID_FILE"
sleep 3

echo "[INFO] Testando health endpoint..."
curl -fsS http://127.0.0.1:18080/health >/tmp/processador-health-postgres.txt

echo "[INFO] Testando endpoint de negocio (POST /api/diagrams)..."
echo "fake image bytes" > /tmp/diagram-test.png
RESPONSE=$(curl -sS -X POST "http://127.0.0.1:18080/api/diagrams" \
  -H "X-Api-Key: dev-client-key" \
  -F "file=@/tmp/diagram-test.png;type=image/png" \
  -F "name=Teste Local" \
  -F "description=Teste com PostgreSQL no Minikube")

echo "$RESPONSE" > /tmp/processador-create-response.json
REQUEST_ID=$(echo "$RESPONSE" | grep -Eo '[0-9a-fA-F-]{36}' | head -1 || true)

if [[ -z "$REQUEST_ID" ]]; then
  echo "[ERROR] Nao foi possivel extrair id da resposta de criacao."
  echo "Resposta: $RESPONSE"
  exit 1
fi

echo "[INFO] Testando endpoint de consulta (GET /api/diagrams/$REQUEST_ID)..."
curl -fsS -H "X-Api-Key: dev-client-key" "http://127.0.0.1:18080/api/diagrams/$REQUEST_ID" > /tmp/processador-get-response.json

echo "[INFO] Simulando retorno do servico interno de processamento..."
curl -fsS -X POST "http://127.0.0.1:18080/internal/testing/diagram-processed" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: dev-internal-key" \
  -d "{\"diagramRequestId\":\"$REQUEST_ID\",\"isSuccess\":true,\"resultUrl\":\"https://reports.local/$REQUEST_ID\"}" \
  > /tmp/processador-simulate-response.json

echo "[INFO] Validando status final analisado..."
curl -fsS -H "X-Api-Key: dev-client-key" "http://127.0.0.1:18080/api/diagrams/$REQUEST_ID" > /tmp/processador-get-analyzed-response.json

if ! grep -q 'Analyzed' /tmp/processador-get-analyzed-response.json; then
  echo "[ERROR] O status final nao foi atualizado para Analyzed."
  echo "Resposta: $(cat /tmp/processador-get-analyzed-response.json)"
  exit 1
fi

echo "[INFO] Testando endpoint de relatorio (GET /api/diagrams/$REQUEST_ID/report)..."
curl -fsS -H "X-Api-Key: dev-client-key" "http://127.0.0.1:18080/api/diagrams/$REQUEST_ID/report" > /tmp/processador-report-response.json

if ! grep -q 'Relatorio tecnico local' /tmp/processador-report-response.json; then
  echo "[ERROR] O endpoint de relatorio nao retornou o payload esperado do mock local."
  echo "Resposta: $(cat /tmp/processador-report-response.json)"
  exit 1
fi

echo "[SUCCESS] Teste completo com PostgreSQL local concluido!"
echo "Health: $(cat /tmp/processador-health-postgres.txt)"
echo "Create response: $(cat /tmp/processador-create-response.json)"
echo "Get response: $(cat /tmp/processador-get-response.json)"
echo "Analyzed response: $(cat /tmp/processador-get-analyzed-response.json)"
echo "Report response: $(cat /tmp/processador-report-response.json)"

echo
kubectl get pods -n "$NAMESPACE"
