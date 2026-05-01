#!/usr/bin/env bash

set -euo pipefail

ACTION="${1:-status}"

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "[ERROR] Variavel obrigatoria ausente: $name" >&2
    exit 1
  fi
}

require_env AWS_REGION
require_env EKS_CLUSTER_NAME

EKS_NODEGROUP_NAME="${EKS_NODEGROUP_NAME:-${EKS_CLUSTER_NAME}-ng}"
EKS_VERSION="${EKS_VERSION:-1.29}"
EKS_NODE_INSTANCE_TYPE="${EKS_NODE_INSTANCE_TYPE:-t3.small}"
EKS_NODE_DISK_SIZE="${EKS_NODE_DISK_SIZE:-20}"
EKS_NODE_DESIRED="${EKS_NODE_DESIRED:-1}"
EKS_NODE_MIN="${EKS_NODE_MIN:-1}"
EKS_NODE_MAX="${EKS_NODE_MAX:-2}"
EKS_CLUSTER_ROLE_ARN="${EKS_CLUSTER_ROLE_ARN:-}"
EKS_NODE_ROLE_ARN="${EKS_NODE_ROLE_ARN:-}"

validate_aws_credentials() {
  local output

  if output="$(aws sts get-caller-identity --output text 2>&1)"; then
    return
  fi

  if echo "$output" | grep -Eq "InvalidClientTokenId|ExpiredToken|UnrecognizedClientException"; then
    echo "[ERROR] Credenciais AWS invalidas ou expiradas." >&2
    echo "[ERROR] Atualize AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_SESSION_TOKEN." >&2
    echo "[ERROR] Detalhe AWS: $output" >&2
    exit 1
  fi

  echo "[ERROR] Falha ao validar credenciais AWS: $output" >&2
  exit 1
}

cluster_exists() {
  aws eks describe-cluster \
    --region "$AWS_REGION" \
    --name "$EKS_CLUSTER_NAME" \
    --query 'cluster.name' \
    --output text >/dev/null 2>&1
}

nodegroup_exists() {
  aws eks describe-nodegroup \
    --region "$AWS_REGION" \
    --cluster-name "$EKS_CLUSTER_NAME" \
    --nodegroup-name "$EKS_NODEGROUP_NAME" \
    --query 'nodegroup.nodegroupName' \
    --output text >/dev/null 2>&1
}

wait_cluster_active() {
  echo "[INFO] Aguardando cluster '$EKS_CLUSTER_NAME' ficar ACTIVE..."
  aws eks wait cluster-active --region "$AWS_REGION" --name "$EKS_CLUSTER_NAME"
}

wait_cluster_visible() {
  local attempts=0
  local max_attempts=40

  while (( attempts < max_attempts )); do
    if cluster_exists; then
      return 0
    fi

    attempts=$((attempts + 1))
    echo "[INFO] Cluster ainda nao visivel. Tentativa $attempts/$max_attempts..."
    sleep 15
  done

  echo "[ERROR] Cluster '$EKS_CLUSTER_NAME' nao ficou visivel apos criacao." >&2
  exit 1
}

wait_nodegroup_active() {
  echo "[INFO] Aguardando nodegroup '$EKS_NODEGROUP_NAME' ficar ACTIVE..."
  aws eks wait nodegroup-active \
    --region "$AWS_REGION" \
    --cluster-name "$EKS_CLUSTER_NAME" \
    --nodegroup-name "$EKS_NODEGROUP_NAME"
}

wait_nodegroup_visible() {
  local attempts=0
  local max_attempts=40

  while (( attempts < max_attempts )); do
    if nodegroup_exists; then
      return 0
    fi

    attempts=$((attempts + 1))
    echo "[INFO] Nodegroup ainda nao visivel. Tentativa $attempts/$max_attempts..."
    sleep 15
  done

  echo "[ERROR] Nodegroup '$EKS_NODEGROUP_NAME' nao ficou visivel apos criacao." >&2
  exit 1
}

wait_nodegroup_deleted() {
  echo "[INFO] Aguardando nodegroup '$EKS_NODEGROUP_NAME' ser removido..."
  aws eks wait nodegroup-deleted \
    --region "$AWS_REGION" \
    --cluster-name "$EKS_CLUSTER_NAME" \
    --nodegroup-name "$EKS_NODEGROUP_NAME"
}

wait_cluster_deleted() {
  echo "[INFO] Aguardando cluster '$EKS_CLUSTER_NAME' ser removido..."
  aws eks wait cluster-deleted --region "$AWS_REGION" --name "$EKS_CLUSTER_NAME"
}

resolve_default_subnets() {
  local default_vpc
  default_vpc="$(aws ec2 describe-vpcs \
    --region "$AWS_REGION" \
    --filters Name=isDefault,Values=true \
    --query 'Vpcs[0].VpcId' \
    --output text)"

  if [[ -z "$default_vpc" || "$default_vpc" == "None" ]]; then
    echo "[ERROR] Nao foi possivel identificar VPC padrao na regiao $AWS_REGION." >&2
    exit 1
  fi

  # EKS pode restringir AZs por conta/regiao. Filtramos para as zonas comumente suportadas.
  local subnet_ids
  subnet_ids="$(aws ec2 describe-subnets \
    --region "$AWS_REGION" \
    --filters Name=vpc-id,Values="$default_vpc" \
    --query "Subnets[?AvailabilityZone=='${AWS_REGION}a' || AvailabilityZone=='${AWS_REGION}b' || AvailabilityZone=='${AWS_REGION}c' || AvailabilityZone=='${AWS_REGION}d' || AvailabilityZone=='${AWS_REGION}f'].SubnetId" \
    --output text | tr '\t' ',')"

  if [[ -z "$subnet_ids" ]]; then
    echo "[ERROR] Nao foi possivel identificar subnets suportadas da VPC padrao '$default_vpc'." >&2
    exit 1
  fi

  local subnet_count
  subnet_count="$(echo "$subnet_ids" | tr ',' '\n' | grep -c . || true)"
  if (( subnet_count < 2 )); then
    echo "[ERROR] Sao necessarias ao menos 2 subnets em AZs suportadas para criar o EKS. Encontradas: $subnet_count" >&2
    exit 1
  fi

  echo "$subnet_ids"
}

resolve_default_role_arns() {
  local account_id
  account_id="$(aws sts get-caller-identity --query 'Account' --output text)"

  if [[ -z "$EKS_CLUSTER_ROLE_ARN" ]]; then
    EKS_CLUSTER_ROLE_ARN="arn:aws:iam::${account_id}:role/LabRole"
  fi

  if [[ -z "$EKS_NODE_ROLE_ARN" ]]; then
    EKS_NODE_ROLE_ARN="$EKS_CLUSTER_ROLE_ARN"
  fi
}

create_cluster() {
  resolve_default_role_arns

  local subnet_ids
  subnet_ids="$(resolve_default_subnets)"

  echo "[INFO] Criando cluster EKS '$EKS_CLUSTER_NAME' na regiao '$AWS_REGION'..."
  echo "[INFO] Subnets: $subnet_ids"
  echo "[INFO] Cluster role ARN: $EKS_CLUSTER_ROLE_ARN"

  aws eks create-cluster \
    --region "$AWS_REGION" \
    --name "$EKS_CLUSTER_NAME" \
    --kubernetes-version "$EKS_VERSION" \
    --role-arn "$EKS_CLUSTER_ROLE_ARN" \
    --resources-vpc-config "subnetIds=$subnet_ids,endpointPublicAccess=true,endpointPrivateAccess=false" >/dev/null

  wait_cluster_visible
  wait_cluster_active
}

create_nodegroup() {
  resolve_default_role_arns

  local subnet_ids
  subnet_ids="$(resolve_default_subnets)"
  local subnet_arr=()
  IFS=',' read -r -a subnet_arr <<< "$subnet_ids"

  echo "[INFO] Criando nodegroup '$EKS_NODEGROUP_NAME'..."
  echo "[INFO] Node role ARN: $EKS_NODE_ROLE_ARN"

  aws eks create-nodegroup \
    --region "$AWS_REGION" \
    --cluster-name "$EKS_CLUSTER_NAME" \
    --nodegroup-name "$EKS_NODEGROUP_NAME" \
    --subnets "${subnet_arr[@]}" \
    --node-role "$EKS_NODE_ROLE_ARN" \
    --scaling-config "minSize=$EKS_NODE_MIN,maxSize=$EKS_NODE_MAX,desiredSize=$EKS_NODE_DESIRED" \
    --instance-types "$EKS_NODE_INSTANCE_TYPE" \
    --disk-size "$EKS_NODE_DISK_SIZE" >/dev/null

  wait_nodegroup_visible
  wait_nodegroup_active
}

ensure_cluster() {
  if ! cluster_exists; then
    create_cluster
  else
    echo "[INFO] Cluster '$EKS_CLUSTER_NAME' ja existe."
  fi

  if ! nodegroup_exists; then
    create_nodegroup
  else
    echo "[INFO] Nodegroup '$EKS_NODEGROUP_NAME' ja existe."
    wait_nodegroup_active
  fi

  output_status
}

pause_cluster() {
  if ! cluster_exists || ! nodegroup_exists; then
    echo "[ERROR] Cluster/nodegroup nao encontrados para pausar." >&2
    exit 1
  fi

  echo "[INFO] Escalando nodegroup '$EKS_NODEGROUP_NAME' para zero (economia de credito EC2)..."
  aws eks update-nodegroup-config \
    --region "$AWS_REGION" \
    --cluster-name "$EKS_CLUSTER_NAME" \
    --nodegroup-name "$EKS_NODEGROUP_NAME" \
    --scaling-config minSize=0,maxSize=1,desiredSize=0 >/dev/null

  wait_nodegroup_active
  output_status
}

resume_cluster() {
  if ! cluster_exists || ! nodegroup_exists; then
    echo "[ERROR] Cluster/nodegroup nao encontrados para retomar." >&2
    exit 1
  fi

  echo "[INFO] Retomando nodegroup '$EKS_NODEGROUP_NAME' para capacidade minima..."
  aws eks update-nodegroup-config \
    --region "$AWS_REGION" \
    --cluster-name "$EKS_CLUSTER_NAME" \
    --nodegroup-name "$EKS_NODEGROUP_NAME" \
    --scaling-config "minSize=$EKS_NODE_MIN,maxSize=$EKS_NODE_MAX,desiredSize=$EKS_NODE_DESIRED" >/dev/null

  wait_nodegroup_active
  output_status
}

delete_cluster() {
  if ! cluster_exists; then
    echo "[INFO] Cluster '$EKS_CLUSTER_NAME' nao existe. Nada para remover."
    return
  fi

  if nodegroup_exists; then
    echo "[INFO] Removendo nodegroup '$EKS_NODEGROUP_NAME'..."
    aws eks delete-nodegroup \
      --region "$AWS_REGION" \
      --cluster-name "$EKS_CLUSTER_NAME" \
      --nodegroup-name "$EKS_NODEGROUP_NAME" >/dev/null
    wait_nodegroup_deleted
  fi

  echo "[INFO] Removendo cluster '$EKS_CLUSTER_NAME'..."
  aws eks delete-cluster --region "$AWS_REGION" --name "$EKS_CLUSTER_NAME" >/dev/null
  wait_cluster_deleted

  echo "[INFO] Cluster removido com sucesso."
}

output_status() {
  if ! cluster_exists; then
    echo "[INFO] Cluster '$EKS_CLUSTER_NAME' nao existe."
    if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
      {
        echo "exists=false"
        echo "cluster_status=not-found"
      } >> "$GITHUB_OUTPUT"
    fi
    return
  fi

  local cluster_status endpoint node_status desired_size min_size max_size
  cluster_status="$(aws eks describe-cluster --region "$AWS_REGION" --name "$EKS_CLUSTER_NAME" --query 'cluster.status' --output text)"
  endpoint="$(aws eks describe-cluster --region "$AWS_REGION" --name "$EKS_CLUSTER_NAME" --query 'cluster.endpoint' --output text)"

  if nodegroup_exists; then
    node_status="$(aws eks describe-nodegroup --region "$AWS_REGION" --cluster-name "$EKS_CLUSTER_NAME" --nodegroup-name "$EKS_NODEGROUP_NAME" --query 'nodegroup.status' --output text)"
    desired_size="$(aws eks describe-nodegroup --region "$AWS_REGION" --cluster-name "$EKS_CLUSTER_NAME" --nodegroup-name "$EKS_NODEGROUP_NAME" --query 'nodegroup.scalingConfig.desiredSize' --output text)"
    min_size="$(aws eks describe-nodegroup --region "$AWS_REGION" --cluster-name "$EKS_CLUSTER_NAME" --nodegroup-name "$EKS_NODEGROUP_NAME" --query 'nodegroup.scalingConfig.minSize' --output text)"
    max_size="$(aws eks describe-nodegroup --region "$AWS_REGION" --cluster-name "$EKS_CLUSTER_NAME" --nodegroup-name "$EKS_NODEGROUP_NAME" --query 'nodegroup.scalingConfig.maxSize' --output text)"
  else
    node_status="not-found"
    desired_size="0"
    min_size="0"
    max_size="0"
  fi

  echo "[INFO] Cluster: $EKS_CLUSTER_NAME"
  echo "[INFO] Status do cluster: $cluster_status"
  echo "[INFO] Endpoint: $endpoint"
  echo "[INFO] Nodegroup: $EKS_NODEGROUP_NAME"
  echo "[INFO] Status do nodegroup: $node_status"
  echo "[INFO] Escala atual (desired/min/max): $desired_size/$min_size/$max_size"

  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    {
      echo "exists=true"
      echo "cluster_status=$cluster_status"
      echo "cluster_endpoint=$endpoint"
      echo "nodegroup_status=$node_status"
      echo "nodegroup_desired=$desired_size"
      echo "nodegroup_min=$min_size"
      echo "nodegroup_max=$max_size"
    } >> "$GITHUB_OUTPUT"
  fi
}

validate_aws_credentials

case "$ACTION" in
  ensure)
    ensure_cluster
    ;;
  pause)
    pause_cluster
    ;;
  resume)
    resume_cluster
    ;;
  delete)
    delete_cluster
    ;;
  status)
    output_status
    ;;
  *)
    echo "Uso: $0 {ensure|pause|resume|delete|status}" >&2
    exit 1
    ;;
esac
