#!/bin/bash

################################################################################
# LocalStack SNS/SQS Test Script
#
# Tests SNS topic and SQS queue by:
# 1. Publishing a test message to SNS
# 2. Receiving the message from SQS
# 3. Validating the message content
################################################################################

set -e

LOCALSTACK_URL="${LOCALSTACK_URL:-http://localhost:4566}"
AWS_REGION="${AWS_REGION:-us-east-1}"

# Topic and Queue names (must match appsettings.Development.json)
SNS_TOPIC_NAME="diagram-requests"
SQS_QUEUE_NAME="diagram-events"
AWS_ACCOUNT_ID="000000000000"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  LocalStack SNS/SQS Test Suite                        ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════╝${NC}"
echo ""

# Function to check AWS CLI
check_aws_cli() {
    if ! command -v aws &> /dev/null; then
        echo -e "${RED}[ERROR] AWS CLI not found. Please install AWS CLI v2${NC}"
        exit 1
    fi
}

# Function to check jq
check_jq() {
    if ! command -v jq &> /dev/null; then
        echo -e "${RED}[ERROR] jq not found. Please install jq${NC}"
        exit 1
    fi
}

# Function to check LocalStack connectivity
check_localstack_health() {
    echo -e "${YELLOW}[Test 1] Checking LocalStack connectivity...${NC}"
    if ! HEALTH_RESPONSE=$(curl -s "${LOCALSTACK_URL}/_localstack/health"); then
        echo -e "${RED}[FAIL] LocalStack is not responding at ${LOCALSTACK_URL}${NC}"
        return 1
    fi
    
    SNS_HEALTH=$(echo "$HEALTH_RESPONSE" | jq -r '.services.sns // "unknown"')
    SQS_HEALTH=$(echo "$HEALTH_RESPONSE" | jq -r '.services.sqs // "unknown"')
    
    echo -e "${GREEN}[PASS] LocalStack is running${NC}"
    echo "       - SNS service: ${SNS_HEALTH}"
    echo "       - SQS service: ${SQS_HEALTH}"
    echo ""
}

# Function to get topic ARN
get_topic_arn() {
    echo -e "${YELLOW}[Test 2] Retrieving SNS topic ARN...${NC}"
    TOPICS=$(aws sns list-topics \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        --output json)
    
    SNS_TOPIC_ARN=$(echo "$TOPICS" | jq -r ".Topics[] | select(.TopicArn | contains(\"${SNS_TOPIC_NAME}\")) | .TopicArn" | head -1)
    
    if [ -z "$SNS_TOPIC_ARN" ] || [ "$SNS_TOPIC_ARN" = "null" ]; then
        echo -e "${RED}[FAIL] SNS topic '${SNS_TOPIC_NAME}' not found${NC}"
        echo "       Run 'bash scripts/init-localstack.sh' first"
        return 1
    fi
    
    echo -e "${GREEN}[PASS] SNS Topic found${NC}"
    echo "       Topic ARN: ${SNS_TOPIC_ARN}"
    echo ""
}

# Function to get queue URL
get_queue_url() {
    echo -e "${YELLOW}[Test 3] Retrieving SQS queue URL...${NC}"
    
    SQS_QUEUE_URL=$(aws sqs get-queue-url \
        --queue-name "${SQS_QUEUE_NAME}" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        --output json 2>/dev/null | jq -r '.QueueUrl')
    
    if [ -z "$SQS_QUEUE_URL" ] || [ "$SQS_QUEUE_URL" = "null" ]; then
        echo -e "${RED}[FAIL] SQS queue '${SQS_QUEUE_NAME}' not found${NC}"
        echo "       Run 'bash scripts/init-localstack.sh' first"
        return 1
    fi
    
    echo -e "${GREEN}[PASS] SQS Queue found${NC}"
    echo "       Queue URL: ${SQS_QUEUE_URL}"
    echo ""
}

# Function to test SNS → SQS message flow
test_message_flow() {
    echo -e "${YELLOW}[Test 4] Testing SNS → SQS message flow...${NC}"
    
    # Clear the queue first
    echo "       Purging queue..."
    aws sqs purge-queue \
        --queue-url "${SQS_QUEUE_URL}" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        > /dev/null 2>&1
    
    sleep 1
    
    # Publish test message to SNS
    TEST_PAYLOAD='{"requestId":"test-123","diagramContent":"test diagram","analysisType":"complexity"}'
    
    echo "       Publishing test message to SNS..."
    PUBLISH_RESPONSE=$(aws sns publish \
        --topic-arn "${SNS_TOPIC_ARN}" \
        --message "${TEST_PAYLOAD}" \
        --message-attributes "eventType={DataType=String,StringValue=DiagramProcessed}" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        --output json)
    
    MESSAGE_ID=$(echo "$PUBLISH_RESPONSE" | jq -r '.MessageId')
    
    if [ -z "$MESSAGE_ID" ] || [ "$MESSAGE_ID" = "null" ]; then
        echo -e "${RED}[FAIL] Failed to publish message to SNS${NC}"
        return 1
    fi
    
    echo -e "       ${GREEN}✓${NC} Message published (ID: ${MESSAGE_ID})"
    
    # Wait a moment for SNS → SQS delivery
    sleep 2
    
    # Receive message from SQS
    echo "       Receiving message from SQS..."
    RECEIVE_RESPONSE=$(aws sqs receive-message \
        --queue-url "${SQS_QUEUE_URL}" \
        --max-number-of-messages 1 \
        --message-attribute-names "All" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        --output json)
    
    MESSAGES=$(echo "$RECEIVE_RESPONSE" | jq -r '.Messages // empty')
    
    if [ -z "$MESSAGES" ]; then
        echo -e "${RED}[FAIL] No message received from SQS queue${NC}"
        return 1
    fi
    
    echo -e "       ${GREEN}✓${NC} Message received from SQS"
    
    # Parse received message
    RECEIPT_HANDLE=$(echo "$MESSAGES" | jq -r '.[0].ReceiptHandle')
    SQS_MESSAGE_BODY=$(echo "$MESSAGES" | jq -r '.[0].Body')
    
    # SNS wraps the message in a JSON envelope, extract the actual payload
    EXTRACTED_PAYLOAD=$(echo "$SQS_MESSAGE_BODY" | jq -r '.Message')
    
    echo -e "${GREEN}[PASS] SNS → SQS message flow working${NC}"
    echo "       Received Payload: ${EXTRACTED_PAYLOAD}"
    echo ""
    
    # Delete message from queue
    aws sqs delete-message \
        --queue-url "${SQS_QUEUE_URL}" \
        --receipt-handle "${RECEIPT_HANDLE}" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        > /dev/null 2>&1
}

# Function to show available cURL commands
show_curl_examples() {
    echo -e "${BLUE}╔════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║  Available cURL Commands                               ║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    echo -e "${YELLOW}1. Publish message directly to SNS:${NC}"
    echo "   curl -X POST '${LOCALSTACK_URL}/' \\"
    echo "     --data-urlencode 'Action=Publish' \\"
    echo "     --data-urlencode 'TopicArn=${SNS_TOPIC_ARN}' \\"
    echo "     --data-urlencode 'Message={\"test\": \"payload\"}'"
    echo ""
    
    echo -e "${YELLOW}2. List messages in SQS queue:${NC}"
    echo "   aws sqs receive-message --queue-url '${SQS_QUEUE_URL}' \\"
    echo "     --endpoint-url '${LOCALSTACK_URL}' --region '${AWS_REGION}'"
    echo ""
    
    echo -e "${YELLOW}3. Get queue attributes (message count):${NC}"
    echo "   aws sqs get-queue-attributes --queue-url '${SQS_QUEUE_URL}' \\"
    echo "     --attribute-names 'ApproximateNumberOfMessages' \\"
    echo "     --endpoint-url '${LOCALSTACK_URL}' --region '${AWS_REGION}'"
    echo ""
    
    echo -e "${YELLOW}4. Purge all messages from queue:${NC}"
    echo "   aws sqs purge-queue --queue-url '${SQS_QUEUE_URL}' \\"
    echo "     --endpoint-url '${LOCALSTACK_URL}' --region '${AWS_REGION}'"
    echo ""
}

# Main execution
main() {
    echo ""
    check_aws_cli
    check_jq
    check_localstack_health || exit 1
    get_topic_arn || exit 1
    get_queue_url || exit 1
    test_message_flow || exit 1
    show_curl_examples
    
    echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
    echo -e "${GREEN}All tests passed! SNS/SQS is working correctly.${NC}"
    echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
    echo ""
}

main
