#!/bin/bash

################################################################################
# LocalStack Initialization Script
# 
# Creates SNS topics and SQS queues required for local development
# Runs automatically after LocalStack container is healthy
################################################################################

set -e

LOCALSTACK_URL="${LOCALSTACK_URL:-http://localhost:4566}"
AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_ACCOUNT_ID="000000000000"

# Topic and Queue names (must match appsettings.Development.json)
SNS_TOPIC_NAME="diagram-requests"
SQS_QUEUE_NAME="diagram-events"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}[LocalStack Init] Starting initialization...${NC}"

# Check if LocalStack is reachable
echo -e "${YELLOW}[LocalStack Init] Checking LocalStack connectivity...${NC}"
if ! curl -s "${LOCALSTACK_URL}/_localstack/health" > /dev/null 2>&1; then
    echo -e "${RED}[LocalStack Init ERROR] LocalStack is not responding at ${LOCALSTACK_URL}${NC}"
    exit 1
fi
echo -e "${GREEN}[LocalStack Init] LocalStack is reachable${NC}"

# --- SNS Topic Creation ---
echo -e "${YELLOW}[LocalStack Init] Creating SNS topic '${SNS_TOPIC_NAME}'...${NC}"

SNS_RESPONSE=$(aws sns create-topic \
    --name "${SNS_TOPIC_NAME}" \
    --region "${AWS_REGION}" \
    --endpoint-url "${LOCALSTACK_URL}" \
  --output text)

SNS_TOPIC_ARN="$SNS_RESPONSE"

if [ -z "$SNS_TOPIC_ARN" ] || [ "$SNS_TOPIC_ARN" = "null" ]; then
    echo -e "${RED}[LocalStack Init ERROR] Failed to create SNS topic${NC}"
    exit 1
fi

echo -e "${GREEN}[LocalStack Init] SNS Topic created: ${SNS_TOPIC_ARN}${NC}"

# --- SQS Queue Creation ---
echo -e "${YELLOW}[LocalStack Init] Creating SQS queue '${SQS_QUEUE_NAME}'...${NC}"

SQS_RESPONSE=$(aws sqs create-queue \
    --queue-name "${SQS_QUEUE_NAME}" \
    --region "${AWS_REGION}" \
    --endpoint-url "${LOCALSTACK_URL}" \
  --output text)

SQS_QUEUE_URL="$SQS_RESPONSE"

if [ -z "$SQS_QUEUE_URL" ] || [ "$SQS_QUEUE_URL" = "null" ]; then
    echo -e "${RED}[LocalStack Init ERROR] Failed to create SQS queue${NC}"
    exit 1
fi

echo -e "${GREEN}[LocalStack Init] SQS Queue created: ${SQS_QUEUE_URL}${NC}"

# --- SNS → SQS Subscription ---
echo -e "${YELLOW}[LocalStack Init] Subscribing SQS queue to SNS topic...${NC}"

SQS_QUEUE_ARN="arn:aws:sqs:${AWS_REGION}:${AWS_ACCOUNT_ID}:${SQS_QUEUE_NAME}"

SUBSCRIBE_RESPONSE=$(aws sns subscribe \
    --topic-arn "${SNS_TOPIC_ARN}" \
    --protocol sqs \
    --notification-endpoint "${SQS_QUEUE_ARN}" \
    --region "${AWS_REGION}" \
    --endpoint-url "${LOCALSTACK_URL}" \
  --output text)

SUBSCRIPTION_ARN="$SUBSCRIBE_RESPONSE"

if [ -z "$SUBSCRIPTION_ARN" ] || [ "$SUBSCRIPTION_ARN" = "null" ]; then
    echo -e "${RED}[LocalStack Init ERROR] Failed to subscribe SQS to SNS${NC}"
    exit 1
fi

echo -e "${GREEN}[LocalStack Init] SNS → SQS subscription created: ${SUBSCRIPTION_ARN}${NC}"

# --- Set SQS Queue Policy (allow SNS to send messages) ---
echo -e "${YELLOW}[LocalStack Init] Setting SQS queue policy to allow SNS messages...${NC}"

QUEUE_POLICY='{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Service": "sns.amazonaws.com"
      },
      "Action": "sqs:SendMessage",
      "Resource": "'"${SQS_QUEUE_ARN}"'",
      "Condition": {
        "ArnEquals": {
          "aws:SourceArn": "'"${SNS_TOPIC_ARN}"'"
        }
      }
    }
  ]
}'

aws sqs set-queue-attributes \
    --queue-url "${SQS_QUEUE_URL}" \
    --attributes Key=Policy,Value="${QUEUE_POLICY}" \
    --region "${AWS_REGION}" \
    --endpoint-url "${LOCALSTACK_URL}" \
    > /dev/null 2>&1

echo -e "${GREEN}[LocalStack Init] SQS queue policy configured${NC}"

# --- Output Summary ---
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}LocalStack Initialization Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "SNS Topic ARN:     ${SNS_TOPIC_ARN}"
echo "SQS Queue URL:     ${SQS_QUEUE_URL}"
echo ""
echo "Expected values in appsettings.Development.json:"
echo "  Aws__TopicArn: ${SNS_TOPIC_ARN}"
echo "  Aws__QueueUrl: ${SQS_QUEUE_URL}"
echo ""
echo "To test SNS/SQS, run:"
echo "  bash scripts/test-sqs-sns-local.sh"
echo ""
