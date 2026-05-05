#!/bin/bash

################################################################################
# End-to-End Test: Create Diagram Request → Monitor SQS
#
# This script:
# 1. Sends a diagram file to the API via multipart/form-data
# 2. Monitors the SQS queue for the resulting message
# 3. Validates the entire SNS/SQS flow
################################################################################

set -e

# Configuration
API_URL="${API_URL:-http://localhost:5000}"
LOCALSTACK_URL="${LOCALSTACK_URL:-http://localhost:4566}"
AWS_REGION="${AWS_REGION:-us-east-1}"
API_KEY="${API_KEY:-dev-client-key}"

SQS_QUEUE_NAME="diagram-events"
AWS_ACCOUNT_ID="000000000000"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  End-to-End SNS/SQS Test                               ║${NC}"
echo -e "${BLUE}║  Create Diagram → Monitor Queue                        ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════╝${NC}"
echo ""

# Function to check if API is running
check_api_health() {
    echo -e "${YELLOW}[Step 1] Checking API health...${NC}"
    
    if ! curl -s "${API_URL}/health" > /dev/null 2>&1; then
        echo -e "${RED}[FAIL] API is not responding at ${API_URL}${NC}"
        echo "       Make sure to run: docker-compose up"
        exit 1
    fi
    
    echo -e "${GREEN}[PASS] API is running at ${API_URL}${NC}"
    echo ""
}

# Function to create test diagram file
create_test_diagram() {
    echo -e "${YELLOW}[Step 2] Creating test diagram file...${NC}"
    
    DIAGRAM_FILE="/tmp/test-diagram.png"
    
    # Create a minimal PNG file (1x1 pixel, white)
    # PNG header + IHDR chunk + IDAT chunk + IEND chunk
    printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x02\x00\x00\x00\x90wS\xde\x00\x00\x00\x0cIDATx\x9cc\xf8\x0f\x00\x00\x01\x01\x00\x05\xd9\xaf\xc3\xf5\x00\x00\x00\x00IEND\xaeB`\x82' > "$DIAGRAM_FILE"
    
    if [ ! -f "$DIAGRAM_FILE" ]; then
        echo -e "${RED}[FAIL] Could not create test diagram file${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}[PASS] Test diagram created at ${DIAGRAM_FILE}${NC}"
    echo ""
}

# Function to get SQS queue URL
get_queue_url() {
    echo -e "${YELLOW}[Step 3] Getting SQS queue URL...${NC}"
    
    SQS_QUEUE_URL=$(aws sqs get-queue-url \
        --queue-name "${SQS_QUEUE_NAME}" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        --output json 2>/dev/null | jq -r '.QueueUrl')
    
    if [ -z "$SQS_QUEUE_URL" ] || [ "$SQS_QUEUE_URL" = "null" ]; then
        echo -e "${RED}[FAIL] SQS queue '${SQS_QUEUE_NAME}' not found${NC}"
        echo "       Run 'bash scripts/init-localstack.sh' first"
        exit 1
    fi
    
    echo -e "${GREEN}[PASS] SQS Queue URL: ${SQS_QUEUE_URL}${NC}"
    echo ""
}

# Function to get message count before
get_initial_message_count() {
    echo -e "${YELLOW}[Step 4] Checking initial message count...${NC}"
    
    INITIAL_COUNT=$(aws sqs get-queue-attributes \
        --queue-url "${SQS_QUEUE_URL}" \
        --attribute-names "ApproximateNumberOfMessages" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        --output json | jq -r '.Attributes.ApproximateNumberOfMessages')
    
    echo -e "${GREEN}[PASS] Initial message count in queue: ${INITIAL_COUNT}${NC}"
    echo ""
}

# Function to create diagram request via API
create_diagram_request() {
    echo -e "${YELLOW}[Step 5] Sending diagram request to API...${NC}"
    
    RESPONSE=$(curl -s -w "\n%{http_code}" \
        -X POST "${API_URL}/api/diagrams" \
        -H "X-Api-Key: ${API_KEY}" \
        -F "file=@${DIAGRAM_FILE}" \
        -F "name=Test Diagram" \
        -F "description=End-to-end SNS/SQS test" \
        2>&1 | tail -2)
    
    HTTP_CODE=$(echo "$RESPONSE" | tail -1)
    RESPONSE_BODY=$(echo "$RESPONSE" | head -1)
    
    if [ "$HTTP_CODE" != "201" ]; then
        echo -e "${RED}[FAIL] API returned HTTP ${HTTP_CODE}${NC}"
        echo "       Response: ${RESPONSE_BODY}"
        exit 1
    fi
    
    REQUEST_ID=$(echo "$RESPONSE_BODY" | jq -r '.requestId // empty')
    
    if [ -z "$REQUEST_ID" ]; then
        echo -e "${RED}[FAIL] Could not extract requestId from API response${NC}"
        echo "       Response: ${RESPONSE_BODY}"
        exit 1
    fi
    
    echo -e "${GREEN}[PASS] Diagram request created (ID: ${REQUEST_ID})${NC}"
    echo "       Response: ${RESPONSE_BODY}"
    echo ""
}

# Function to monitor SQS queue
monitor_queue() {
    echo -e "${YELLOW}[Step 6] Monitoring SQS queue for messages...${NC}"
    echo "       Waiting up to 10 seconds for message to arrive..."
    echo ""
    
    ELAPSED=0
    MAX_WAIT=10
    FOUND_MESSAGE=false
    
    while [ $ELAPSED -lt $MAX_WAIT ]; do
        CURRENT_COUNT=$(aws sqs get-queue-attributes \
            --queue-url "${SQS_QUEUE_URL}" \
            --attribute-names "ApproximateNumberOfMessages" \
            --region "${AWS_REGION}" \
            --endpoint-url "${LOCALSTACK_URL}" \
            --output json | jq -r '.Attributes.ApproximateNumberOfMessages')
        
        if [ "$CURRENT_COUNT" -gt "$INITIAL_COUNT" ]; then
            echo -e "       ${GREEN}✓${NC} Message appeared in queue after ${ELAPSED}s"
            FOUND_MESSAGE=true
            break
        fi
        
        echo -ne "       Current count: ${CURRENT_COUNT} (${ELAPSED}s elapsed)\r"
        sleep 1
        ELAPSED=$((ELAPSED + 1))
    done
    
    echo ""
    
    if [ "$FOUND_MESSAGE" = false ]; then
        echo -e "${RED}[FAIL] No message appeared in queue after ${MAX_WAIT} seconds${NC}"
        echo "       This could mean:"
        echo "       - API is not published to SNS"
        echo "       - SNS → SQS subscription not working"
        echo "       - docker-compose EnableAwsServices is not set to true"
        return 1
    fi
    
    echo ""
}

# Function to retrieve and validate message
validate_message() {
    echo -e "${YELLOW}[Step 7] Retrieving message from SQS...${NC}"
    
    MESSAGE=$(aws sqs receive-message \
        --queue-url "${SQS_QUEUE_URL}" \
        --max-number-of-messages 1 \
        --message-attribute-names "All" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        --output json)
    
    if [ -z "$(echo "$MESSAGE" | jq -r '.Messages // empty')" ]; then
        echo -e "${RED}[FAIL] No message found in queue${NC}"
        return 1
    fi
    
    RECEIPT_HANDLE=$(echo "$MESSAGE" | jq -r '.Messages[0].ReceiptHandle')
    MESSAGE_BODY=$(echo "$MESSAGE" | jq -r '.Messages[0].Body')
    
    # SNS wraps in envelope, extract actual payload
    PAYLOAD=$(echo "$MESSAGE_BODY" | jq -r '.Message')
    EVENT_TYPE=$(echo "$MESSAGE_BODY" | jq -r '.MessageAttributes.eventType.Value // "Unknown"')
    
    echo -e "${GREEN}[PASS] Message retrieved from SQS${NC}"
    echo "       Event Type: ${EVENT_TYPE}"
    echo "       Payload: ${PAYLOAD}"
    echo ""
    
    # Delete message
    aws sqs delete-message \
        --queue-url "${SQS_QUEUE_URL}" \
        --receipt-handle "${RECEIPT_HANDLE}" \
        --region "${AWS_REGION}" \
        --endpoint-url "${LOCALSTACK_URL}" \
        > /dev/null 2>&1
    
    return 0
}

# Main execution
main() {
    echo ""
    check_api_health
    create_test_diagram
    get_queue_url
    get_initial_message_count
    create_diagram_request
    
    # Wait a bit for background processing
    sleep 2
    
    monitor_queue || {
        echo -e "${RED}════════════════════════════════════════════════════════${NC}"
        echo -e "${RED}Test FAILED: Message did not arrive in queue${NC}"
        echo -e "${RED}════════════════════════════════════════════════════════${NC}"
        exit 1
    }
    
    validate_message || {
        echo -e "${RED}════════════════════════════════════════════════════════${NC}"
        echo -e "${RED}Test FAILED: Could not validate message${NC}"
        echo -e "${RED}════════════════════════════════════════════════════════${NC}"
        exit 1
    }
    
    echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
    echo -e "${GREEN}End-to-End Test PASSED!${NC}"
    echo -e "${GREEN}────────────────────────────────────────────────${NC}"
    echo -e "${GREEN}Flow: API → Outbox → SNS → SQS → Inbox${NC}"
    echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
    echo ""
}

main
