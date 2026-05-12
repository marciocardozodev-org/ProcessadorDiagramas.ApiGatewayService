#!/usr/bin/env bash

set -euo pipefail

AWS_REGION="${AWS_REGION:-us-east-1}"
EKS_CLUSTER_NAME="${EKS_CLUSTER_NAME:-processador-diagramas-shared-eks}"
EKS_NODEGROUP_NAME="${EKS_NODEGROUP_NAME:-processador-diagramas-shared-eks-ng}"
DB_INSTANCE_IDENTIFIER="${DB_INSTANCE_IDENTIFIER:-processador-diagramas-pg-hml}"

source scripts/aws-managed-resources.sh

require_aws_auth() {
  if ! aws sts get-caller-identity --output text >/dev/null 2>&1; then
    echo "[ERROR] Credenciais AWS invalidas/expiradas. Atualize AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_SESSION_TOKEN." >&2
    exit 1
  fi
}

echo "[INFO] Rotina de INICIO DO DIA (restaurar ambiente)"
echo "[INFO] Regiao: $AWS_REGION"
echo "[INFO] Cluster EKS: $EKS_CLUSTER_NAME"
echo "[INFO] Nodegroup EKS: $EKS_NODEGROUP_NAME"
echo "[INFO] RDS: $DB_INSTANCE_IDENTIFIER"

print_managed_resource_inventory

require_aws_auth

# 1) Recria/garante EKS + nodegroup
AWS_REGION="$AWS_REGION" \
EKS_CLUSTER_NAME="$EKS_CLUSTER_NAME" \
EKS_NODEGROUP_NAME="$EKS_NODEGROUP_NAME" \
bash scripts/eks-manage.sh ensure

# 2) Inicia/garante RDS
AWS_REGION="$AWS_REGION" \
DB_INSTANCE_IDENTIFIER="$DB_INSTANCE_IDENTIFIER" \
bash scripts/rds-manage.sh ensure

# 3) Garante os recursos de mensageria compartilhados pelos microservicos do ambiente
AWS_REGION="$AWS_REGION" \
ensure_core_messaging_resources

# 4) Atualiza kubeconfig local
aws eks update-kubeconfig --region "$AWS_REGION" --name "$EKS_CLUSTER_NAME"

echo "[INFO] Ambiente restaurado: EKS e RDS prontos para deploy/testes."
