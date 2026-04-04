#!/bin/bash

# Cleanup de recursos locais Minikube
# Remove cluster local e libera recursos

set -e

echo "[INFO] Removendo cluster Minikube..."
minikube delete --all

echo "[INFO] Limpando arquivos residuais..."
rm -rf ~/.minikube ~/.kube/config 2>/dev/null || true

echo "[SUCCESS] Cleanup concluído!"
echo ""
echo "Máquina local liberada. Próximos passos para AWS:"
echo "1. Renovar credenciais do AWS Academy"
echo "2. Fazer merge develop -> homolog"
echo "3. Pipeline criará o cluster EKS com nós"
