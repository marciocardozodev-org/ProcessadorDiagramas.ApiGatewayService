#!/usr/bin/env bash

set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
EKS_CLUSTER_NAME="${EKS_CLUSTER_NAME:-processador-diagramas-shared-eks}"
EKS_NODEGROUP_NAME="${EKS_NODEGROUP_NAME:-processador-diagramas-shared-eks-ng}"
DB_INSTANCE_IDENTIFIER="${DB_INSTANCE_IDENTIFIER:-processador-diagramas-pg-hml}"

require_aws_auth() {
  if ! aws sts get-caller-identity --output text >/dev/null 2>&1; then
    echo "[ERROR] Credenciais AWS invalidas/expiradas. Atualize AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_SESSION_TOKEN." >&2
    exit 1
  fi
}

echo "[INFO] Rotina de FIM DO DIA (economia maxima)"
echo "[INFO] Regiao: $AWS_REGION"
echo "[INFO] Cluster EKS: $EKS_CLUSTER_NAME"
echo "[INFO] Nodegroup EKS: $EKS_NODEGROUP_NAME"
echo "[INFO] RDS: $DB_INSTANCE_IDENTIFIER"

require_aws_auth

# 1) Remove cluster EKS (maior economia para overnight)
AWS_REGION="$AWS_REGION" \
EKS_CLUSTER_NAME="$EKS_CLUSTER_NAME" \
EKS_NODEGROUP_NAME="$EKS_NODEGROUP_NAME" \
bash scripts/eks-manage.sh delete

# 2) Para o RDS (economiza compute)
AWS_REGION="$AWS_REGION" \
DB_INSTANCE_IDENTIFIER="$DB_INSTANCE_IDENTIFIER" \
bash scripts/rds-manage.sh stop

echo "[INFO] Economia maxima aplicada: EKS removido e RDS parado."
