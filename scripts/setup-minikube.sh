#!/bin/bash

# Setup Minikube para desenvolvimento local
# Script para preparar ambiente local antes de testar na AWS EKS

set -euo pipefail

MINIKUBE_BIN="/usr/local/bin/minikube"
MINIKUBE_PROFILE="processador-dev"
TMP_BIN="/tmp/minikube-linux-amd64"

require_cmd() {
	local cmd="$1"
	if ! command -v "$cmd" >/dev/null 2>&1; then
		echo "[ERROR] Comando obrigatório não encontrado: $cmd"
		exit 1
	fi
}

install_minikube() {
	echo "[INFO] Instalando/Reinstalando Minikube..."
	rm -f "$TMP_BIN"
	curl -fL --retry 5 --retry-all-errors --connect-timeout 15 \
		-o "$TMP_BIN" \
		"https://github.com/kubernetes/minikube/releases/latest/download/minikube-linux-amd64"

	if ! file "$TMP_BIN" | grep -q "ELF"; then
		echo "[ERROR] Download inválido do Minikube (conteúdo não é binário ELF)."
		echo "[ERROR] Tente novamente em alguns minutos."
		exit 1
	fi

	sudo install "$TMP_BIN" "$MINIKUBE_BIN"
	rm -f "$TMP_BIN"

	# Valida execução do binário recém instalado.
	"$MINIKUBE_BIN" version >/dev/null
	echo "[INFO] Minikube instalado com sucesso."
}

echo "[INFO] Validando pré-requisitos..."
require_cmd docker
require_cmd kubectl
require_cmd curl
require_cmd file

if ! command -v minikube >/dev/null 2>&1; then
	install_minikube
else
	if ! minikube version >/dev/null 2>&1; then
		echo "[WARN] Binário do Minikube está inválido. Reinstalando..."
		install_minikube
	fi
fi

echo "[INFO] Iniciando cluster Minikube (profile: $MINIKUBE_PROFILE)..."
minikube start --profile "$MINIKUBE_PROFILE" --cpus 2 --memory 2048 --driver=docker

echo "[INFO] Aguardando cluster ficar pronto..."
kubectl wait --for=condition=Ready node/"$MINIKUBE_PROFILE" --timeout=300s || true

echo "[INFO] Verificando status..."
minikube status --profile "$MINIKUBE_PROFILE"
kubectl get nodes
kubectl get pods -A

echo "[SUCCESS] Minikube pronto para desenvolvimento!"
echo ""
echo "Próximos passos:"
echo "1. Build da aplicação: dotnet build"
echo "2. Build da imagem Docker: docker build -t processador-diagramas:dev ."
echo "3. Carregar imagem no Minikube: minikube image load processador-diagramas:dev --profile $MINIKUBE_PROFILE"
echo "4. Deploy local (manifesto local): ./scripts/test-minikube-local.sh"
echo ""
echo "Para cleanup:"
echo "  ./scripts/cleanup-minikube.sh"
