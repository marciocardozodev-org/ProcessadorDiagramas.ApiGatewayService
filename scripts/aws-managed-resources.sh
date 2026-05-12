#!/usr/bin/env bash

set -euo pipefail

MANAGED_EKS_CLUSTER_NAME="${MANAGED_EKS_CLUSTER_NAME:-processador-diagramas-shared-eks}"
MANAGED_EKS_NODEGROUP_NAME="${MANAGED_EKS_NODEGROUP_NAME:-processador-diagramas-shared-eks-ng}"
MANAGED_RDS_INSTANCE_IDENTIFIER="${MANAGED_RDS_INSTANCE_IDENTIFIER:-processador-diagramas-pg-hml}"

MANAGED_SNS_TOPICS=(
  "RedshiftSNS"
  "processador-diagramas-processingservice-events"
  "processador-diagramas-processingservice-hml-topic"
)

MANAGED_SQS_QUEUES=(
  "processador-diagramas-processingservice-hml-queue"
  "processador-diagramas-processingservice-input"
  "upload-orchestrator-analysis-completed"
  "upload-orchestrator-analysis-requested"
)

print_managed_resource_inventory() {
  echo "[INFO] Inventario de recursos gerenciados"
  echo "[INFO] EKS cluster: $MANAGED_EKS_CLUSTER_NAME"
  echo "[INFO] EKS nodegroup: $MANAGED_EKS_NODEGROUP_NAME"
  echo "[INFO] RDS instance: $MANAGED_RDS_INSTANCE_IDENTIFIER"
  echo "[INFO] SNS topics: ${MANAGED_SNS_TOPICS[*]}"
  echo "[INFO] SQS queues: ${MANAGED_SQS_QUEUES[*]}"
}

topic_arn_by_name() {
  local topic_name="$1"

  aws sns list-topics \
    --region "$AWS_REGION" \
    --query "Topics[?ends_with(TopicArn, ':${topic_name}')].TopicArn | [0]" \
    --output text 2>/dev/null || true
}

queue_url_by_name() {
  local queue_name="$1"

  aws sqs get-queue-url \
    --region "$AWS_REGION" \
    --queue-name "$queue_name" \
    --query 'QueueUrl' \
    --output text 2>/dev/null || true
}

queue_arn_by_url() {
  local queue_url="$1"

  aws sqs get-queue-attributes \
    --region "$AWS_REGION" \
    --queue-url "$queue_url" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text 2>/dev/null || true
}

ensure_core_messaging_resources() {
  local topic_name="processador-diagramas-processingservice-hml-topic"
  local queue_name="processador-diagramas-processingservice-hml-queue"

  local topic_arn
  topic_arn="$(topic_arn_by_name "$topic_name")"
  if [[ -z "$topic_arn" || "$topic_arn" == "None" ]]; then
    echo "[INFO] Criando topico SNS '$topic_name'..."
    topic_arn="$(aws sns create-topic --region "$AWS_REGION" --name "$topic_name" --query 'TopicArn' --output text)"
  fi

  local queue_url
  queue_url="$(queue_url_by_name "$queue_name")"
  if [[ -z "$queue_url" || "$queue_url" == "None" ]]; then
    echo "[INFO] Criando fila SQS '$queue_name'..."
    queue_url="$(aws sqs create-queue --region "$AWS_REGION" --queue-name "$queue_name" --query 'QueueUrl' --output text)"
  fi

  local queue_arn
  queue_arn="$(queue_arn_by_url "$queue_url")"

  echo "[INFO] Garantindo assinatura SNS -> SQS para '$topic_name' / '$queue_name'..."
  aws sns subscribe \
    --region "$AWS_REGION" \
    --topic-arn "$topic_arn" \
    --protocol sqs \
    --notification-endpoint "$queue_arn" \
    >/dev/null

  local queue_policy
  queue_policy=$(printf '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"sns.amazonaws.com"},"Action":"sqs:SendMessage","Resource":"%s","Condition":{"ArnEquals":{"aws:SourceArn":"%s"}}}]}' "$queue_arn" "$topic_arn")

  aws sqs set-queue-attributes \
    --region "$AWS_REGION" \
    --queue-url "$queue_url" \
    --attributes "Policy=$queue_policy" \
    >/dev/null

  echo "[INFO] Recursos de mensageria garantidos:"
  echo "[INFO] Topico SNS: $topic_arn"
  echo "[INFO] Fila SQS: $queue_url"
}

delete_sns_topic_if_exists() {
  local topic_name="$1"
  local topic_arn

  topic_arn="$(topic_arn_by_name "$topic_name")"
  if [[ -z "$topic_arn" || "$topic_arn" == "None" ]]; then
    echo "[INFO] Topico SNS '$topic_name' nao encontrado."
    return 0
  fi

  echo "[INFO] Removendo topico SNS '$topic_name'..."
  if ! aws sns delete-topic --region "$AWS_REGION" --topic-arn "$topic_arn" >/dev/null 2>&1; then
    echo "[WARN] Nao foi possivel remover o topico SNS '$topic_name'. O IAM bloqueou a operacao."
  fi
}

delete_sqs_queue_if_exists() {
  local queue_name="$1"
  local queue_url

  queue_url="$(queue_url_by_name "$queue_name")"
  if [[ -z "$queue_url" || "$queue_url" == "None" ]]; then
    echo "[INFO] Fila SQS '$queue_name' nao encontrada."
    return 0
  fi

  echo "[INFO] Removendo fila SQS '$queue_name'..."
  if ! aws sqs delete-queue --region "$AWS_REGION" --queue-url "$queue_url" >/dev/null 2>&1; then
    echo "[WARN] Nao foi possivel remover a fila SQS '$queue_name'. O IAM bloqueou a operacao."
  fi
}

delete_managed_messaging_resources() {
  local topic_name queue_name

  for topic_name in "${MANAGED_SNS_TOPICS[@]}"; do
    delete_sns_topic_if_exists "$topic_name"
  done

  for queue_name in "${MANAGED_SQS_QUEUES[@]}"; do
    delete_sqs_queue_if_exists "$queue_name"
  done
}