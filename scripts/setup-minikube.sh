#!/bin/bash

# Setup Minikube para desenvolvimento local
# Script para preparar ambiente local antes de testar na AWS EKS

set -e

echo "[INFO] Instalando Minikube..."
curl -LO https://github.com/kubernetes/minikube/releases/latest/download/minikube-linux-amd64
sudo install minikube-linux-amd64 /usr/local/bin/minikube
rm minikube-linux-amd64

echo "[INFO] Iniciando cluster Minikube..."
minikube start --cpus 2 --memory 2048 --driver=docker

echo "[INFO] Aguardando cluster ficar pronto..."
kubectl wait --for=condition=Ready node/minikube --timeout=300s || true

echo "[INFO] Verificando status..."
kubectl get nodes
kubectl get pods -A

echo "[SUCCESS] Minikube pronto para desenvolvimento!"
echo ""
echo "Próximos passos:"
echo "1. Build da aplicação: dotnet build"
echo "2. Build da imagem Docker: docker build -t processador-diagramas:local ."
echo "3. Deploy local: kubectl apply -f deploy/k8s/deployment.yaml"
echo "4. Verificar pods: kubectl get pods"
echo "5. Port-forward (opcional): kubectl port-forward svc/processador-diagramas-apigatewayservice 8080:8080"
echo ""
echo "Para cleanup:"
echo "  ./scripts/cleanup-minikube.sh"
