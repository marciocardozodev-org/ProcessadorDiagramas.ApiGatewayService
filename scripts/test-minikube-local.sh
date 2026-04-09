#!/bin/bash

# Teste local da API no Minikube sem dependencias AWS reais.

set -euo pipefail

NAMESPACE="app"
PROFILE="processador-dev"
IMAGE="processador-diagramas:dev"
MANIFEST="/tmp/processador-minikube-local.yaml"

cat > "$MANIFEST" <<'YAML'
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
  ConnectionStrings__DefaultConnection: "Host=localhost;Port=5432;Database=ProcessadorDiagramas;Username=postgres;Password=postgres"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: processador-diagramas-apigatewayservice
  namespace: app
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
        image: processador-diagramas:dev
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: Development
        - name: ASPNETCORE_URLS
          value: http://+:8080
        - name: EnableAwsServices
          value: "false"
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 15
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

echo "[INFO] Build .NET..."
dotnet build

echo "[INFO] Build imagem Docker..."
docker build -t "$IMAGE" -f Dockerfile .

echo "[INFO] Carregando imagem no Minikube..."
minikube image load "$IMAGE" --profile "$PROFILE"

echo "[INFO] Criando namespace..."
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

echo "[INFO] Aplicando manifesto local..."
kubectl apply -f "$MANIFEST"

echo "[INFO] Aguardando deployment ficar disponível..."
kubectl rollout status deployment/processador-diagramas-apigatewayservice -n "$NAMESPACE" --timeout=180s
kubectl wait --for=condition=available deployment/processador-diagramas-apigatewayservice -n "$NAMESPACE" --timeout=180s

echo "[INFO] Health check interno via port-forward..."
kubectl port-forward -n "$NAMESPACE" svc/processador-diagramas-apigatewayservice 18080:80 >/tmp/processador-port-forward.log 2>&1 &
PF_PID=$!
trap 'kill $PF_PID >/dev/null 2>&1 || true' EXIT
sleep 3
curl -fsS http://127.0.0.1:18080/health >/tmp/processador-health.txt

echo "[SUCCESS] Teste local OK!"
kubectl get pods -n "$NAMESPACE"
echo "Health: $(cat /tmp/processador-health.txt)"
echo "Observacao: para validar fluxo funcional completo use ./scripts/test-minikube-postgres.sh"
