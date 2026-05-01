#!/bin/bash

# Cleanup de recursos locais Minikube
# Remove cluster local e libera recursos

set -euo pipefail

MINIKUBE_PROFILE="processador-dev"

echo "[INFO] Removendo cluster Minikube (profile: $MINIKUBE_PROFILE)..."
minikube delete --profile "$MINIKUBE_PROFILE" || true

echo "[INFO] Removendo cache de imagem do profile..."
minikube image ls --profile "$MINIKUBE_PROFILE" >/dev/null 2>&1 || true

echo "[SUCCESS] Cleanup concluído!"
echo ""
echo "Máquina local liberada. Próximos passos para AWS:"
echo "1. Renovar credenciais do AWS Academy"
echo "2. Fazer merge develop -> homolog"
echo "3. Pipeline criará o cluster EKS com nós"
