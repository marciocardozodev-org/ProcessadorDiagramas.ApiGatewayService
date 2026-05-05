#!/usr/bin/env bash

set -euo pipefail

ACTION="${1:-ensure}"

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "[ERROR] Variavel obrigatoria ausente: $name" >&2
    exit 1
  fi
}

require_env AWS_REGION
require_env DB_INSTANCE_IDENTIFIER

DB_ENGINE="${DB_ENGINE:-postgres}"
DB_ENGINE_VERSION="${DB_ENGINE_VERSION:-}"
DB_INSTANCE_CLASS="${DB_INSTANCE_CLASS:-db.t3.micro}"
DB_ALLOCATED_STORAGE="${DB_ALLOCATED_STORAGE:-20}"
DB_STORAGE_TYPE="${DB_STORAGE_TYPE:-gp3}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-processador_diagramas}"
DB_BACKUP_RETENTION_DAYS="${DB_BACKUP_RETENTION_DAYS:-1}"
DB_PUBLICLY_ACCESSIBLE="${DB_PUBLICLY_ACCESSIBLE:-false}"
DB_MULTI_AZ="${DB_MULTI_AZ:-false}"
DB_AUTO_MINOR_VERSION_UPGRADE="${DB_AUTO_MINOR_VERSION_UPGRADE:-false}"

validate_aws_credentials() {
  local output

  if output="$(aws sts get-caller-identity --output text 2>&1)"; then
    return
  fi

  if echo "$output" | grep -Eq "InvalidClientTokenId|ExpiredToken|UnrecognizedClientException"; then
    echo "[ERROR] Credenciais AWS invalidas ou expiradas." >&2
    echo "[ERROR] Atualize AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_SESSION_TOKEN nos Secrets do GitHub." >&2
    echo "[ERROR] Detalhe AWS: $output" >&2
    exit 1
  fi

  echo "[ERROR] Falha ao validar credenciais AWS: $output" >&2
  exit 1
}

db_exists() {
  local output

  if output="$(aws rds describe-db-instances \
    --region "$AWS_REGION" \
    --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" \
    --query 'DBInstances[0].DBInstanceIdentifier' \
    --output text 2>&1)"; then
    return 0
  fi

  if echo "$output" | grep -q "DBInstanceNotFound"; then
    return 1
  fi

  if echo "$output" | grep -Eq "InvalidClientTokenId|ExpiredToken|UnrecognizedClientException"; then
    echo "[ERROR] Credenciais AWS invalidas ou expiradas durante consulta da instancia RDS." >&2
    echo "[ERROR] Atualize AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY e AWS_SESSION_TOKEN nos Secrets do GitHub." >&2
    echo "[ERROR] Detalhe AWS: $output" >&2
    exit 1
  fi

  echo "[ERROR] Falha inesperada ao consultar instancia '$DB_INSTANCE_IDENTIFIER': $output" >&2
  exit 1
}

db_status() {
  aws rds describe-db-instances \
    --region "$AWS_REGION" \
    --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" \
    --query 'DBInstances[0].DBInstanceStatus' \
    --output text
}

wait_available() {
  echo "[INFO] Aguardando instancia '$DB_INSTANCE_IDENTIFIER' ficar disponivel..."
  aws rds wait db-instance-available \
    --region "$AWS_REGION" \
    --db-instance-identifier "$DB_INSTANCE_IDENTIFIER"
}

wait_stopped() {
  echo "[INFO] Aguardando instancia '$DB_INSTANCE_IDENTIFIER' parar..."
  local status
  local attempts=0
  local max_attempts=120

  while (( attempts < max_attempts )); do
    status="$(db_status)"
    if [[ "$status" == "stopped" ]]; then
      return
    fi

    attempts=$((attempts + 1))
    echo "[INFO] Status atual do RDS: $status (tentativa $attempts/$max_attempts)"
    sleep 15
  done

  echo "[ERROR] Timeout aguardando RDS parar. Status final: $status" >&2
  exit 1
}

create_db() {
  require_env DB_MASTER_USERNAME
  require_env DB_MASTER_PASSWORD
  require_env DB_SUBNET_GROUP_NAME
  require_env DB_VPC_SECURITY_GROUP_IDS

  local multi_az_arg="--no-multi-az"
  local public_arg="--no-publicly-accessible"
  local auto_minor_arg="--no-auto-minor-version-upgrade"

  if [[ "$DB_MULTI_AZ" == "true" ]]; then
    multi_az_arg="--multi-az"
  fi

  if [[ "$DB_PUBLICLY_ACCESSIBLE" == "true" ]]; then
    public_arg="--publicly-accessible"
  fi

  if [[ "$DB_AUTO_MINOR_VERSION_UPGRADE" == "true" ]]; then
    auto_minor_arg="--auto-minor-version-upgrade"
  fi

  if [[ -z "$DB_ENGINE_VERSION" ]]; then
    DB_ENGINE_VERSION="$(aws rds describe-db-engine-versions \
      --region "$AWS_REGION" \
      --engine "$DB_ENGINE" \
      --default-only \
      --query 'DBEngineVersions[0].EngineVersion' \
      --output text)"
  fi

  IFS=',' read -r -a sg_ids <<< "$DB_VPC_SECURITY_GROUP_IDS"

  echo "[INFO] Criando RDS PostgreSQL economico ($DB_INSTANCE_CLASS, ${DB_ALLOCATED_STORAGE}GiB, single-AZ)..."
  aws rds create-db-instance \
    --region "$AWS_REGION" \
    --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" \
    --engine "$DB_ENGINE" \
    --engine-version "$DB_ENGINE_VERSION" \
    --db-instance-class "$DB_INSTANCE_CLASS" \
    --allocated-storage "$DB_ALLOCATED_STORAGE" \
    --storage-type "$DB_STORAGE_TYPE" \
    --master-username "$DB_MASTER_USERNAME" \
    --master-user-password "$DB_MASTER_PASSWORD" \
    --db-name "$DB_NAME" \
    --port "$DB_PORT" \
    --backup-retention-period "$DB_BACKUP_RETENTION_DAYS" \
    --db-subnet-group-name "$DB_SUBNET_GROUP_NAME" \
    --vpc-security-group-ids "${sg_ids[@]}" \
    --no-deletion-protection \
    --no-enable-performance-insights \
    --copy-tags-to-snapshot \
    $multi_az_arg \
    $public_arg \
    $auto_minor_arg >/dev/null

  wait_available
}

ensure_db() {
  if ! db_exists; then
    create_db
    return
  fi

  local status
  status="$(db_status)"
  echo "[INFO] Instancia existente encontrada com status: $status"

  case "$status" in
    available)
      ;;
    stopped)
      echo "[INFO] Iniciando instancia parada..."
      aws rds start-db-instance \
        --region "$AWS_REGION" \
        --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" >/dev/null
      wait_available
      ;;
    stopping)
      wait_stopped
      echo "[INFO] Iniciando instancia apos parada..."
      aws rds start-db-instance \
        --region "$AWS_REGION" \
        --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" >/dev/null
      wait_available
      ;;
    creating|starting|backing-up|modifying|configuring-enhanced-monitoring|maintenance)
      wait_available
      ;;
    *)
      echo "[ERROR] Status nao suportado para ensure: $status" >&2
      exit 1
      ;;
  esac
}

start_db() {
  if ! db_exists; then
    echo "[ERROR] Instancia '$DB_INSTANCE_IDENTIFIER' nao existe para iniciar." >&2
    exit 1
  fi

  local status
  status="$(db_status)"
  case "$status" in
    available)
      echo "[INFO] Instancia ja esta em execucao."
      ;;
    stopped)
      aws rds start-db-instance --region "$AWS_REGION" --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" >/dev/null
      wait_available
      ;;
    *)
      wait_available
      ;;
  esac
}

stop_db() {
  if ! db_exists; then
    echo "[INFO] Instancia '$DB_INSTANCE_IDENTIFIER' nao existe. Nada para parar."
    return
  fi

  local status
  status="$(db_status)"
  case "$status" in
    stopped)
      echo "[INFO] Instancia ja esta parada."
      ;;
    available)
      aws rds stop-db-instance --region "$AWS_REGION" --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" >/dev/null
      wait_stopped
      ;;
    stopping)
      wait_stopped
      ;;
    *)
      echo "[ERROR] Nao e possivel parar agora. Status atual: $status" >&2
      exit 1
      ;;
  esac
}

output_db_info() {
  local endpoint port db_name_out master_username status

  endpoint="$(aws rds describe-db-instances --region "$AWS_REGION" --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" --query 'DBInstances[0].Endpoint.Address' --output text)"
  port="$(aws rds describe-db-instances --region "$AWS_REGION" --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" --query 'DBInstances[0].Endpoint.Port' --output text)"
  db_name_out="$(aws rds describe-db-instances --region "$AWS_REGION" --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" --query 'DBInstances[0].DBName' --output text)"
  master_username="$(aws rds describe-db-instances --region "$AWS_REGION" --db-instance-identifier "$DB_INSTANCE_IDENTIFIER" --query 'DBInstances[0].MasterUsername' --output text)"
  status="$(db_status)"

  echo "[INFO] RDS endpoint: $endpoint:$port"
  echo "[INFO] RDS db name: $db_name_out"
  echo "[INFO] RDS master username: $master_username"
  echo "[INFO] RDS status: $status"

  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    {
      echo "endpoint=$endpoint"
      echo "port=$port"
      echo "db_name=$db_name_out"
      echo "master_username=$master_username"
      echo "status=$status"
    } >> "$GITHUB_OUTPUT"
  fi
}

case "$ACTION" in
  ensure)
    validate_aws_credentials
    ensure_db
    output_db_info
    ;;
  start)
    validate_aws_credentials
    start_db
    output_db_info
    ;;
  stop)
    validate_aws_credentials
    stop_db
    if db_exists; then
      output_db_info
    fi
    ;;
  status)
    validate_aws_credentials
    if db_exists; then
      output_db_info
    else
      echo "[INFO] Instancia '$DB_INSTANCE_IDENTIFIER' nao existe."
      if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
        echo "status=not-found" >> "$GITHUB_OUTPUT"
      fi
    fi
    ;;
  *)
    echo "Uso: $0 {ensure|start|stop|status}" >&2
    exit 1
    ;;
esac
