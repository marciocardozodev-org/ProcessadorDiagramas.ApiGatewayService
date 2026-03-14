using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

using SnsMessageAttributeValue = Amazon.SimpleNotificationService.Model.MessageAttributeValue;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Messaging;

public sealed class AwsMessageBus : IMessageBus
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IAmazonSQS _sqs;
    private readonly AwsSettings _settings;
    private readonly ILogger<AwsMessageBus> _logger;

    public AwsMessageBus(
        IAmazonSimpleNotificationService sns,
        IAmazonSQS sqs,
        IOptions<AwsSettings> settings,
        ILogger<AwsMessageBus> logger)
    {
        _sns = sns;
        _sqs = sqs;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        var request = new PublishRequest
        {
            TopicArn = _settings.TopicArn,
            Message = payload,
            MessageAttributes = new Dictionary<string, SnsMessageAttributeValue>
            {
                ["eventType"] = new SnsMessageAttributeValue
                {
                    DataType = "String",
                    StringValue = eventType
                }
            }
        };

        await _sns.PublishAsync(request, cancellationToken);
        _logger.LogInformation("Published event '{EventType}' to SNS topic {TopicArn}.", eventType, _settings.TopicArn);
    }

    public async Task SubscribeAsync(
        Func<BusMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var receiveRequest = new ReceiveMessageRequest
            {
                QueueUrl = _settings.QueueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 20,
                MessageAttributeNames = ["All"]
            };

            ReceiveMessageResponse response;
            try
            {
                response = await _sqs.ReceiveMessageAsync(receiveRequest, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error polling SQS queue {QueueUrl}.", _settings.QueueUrl);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                continue;
            }

            foreach (var sqsMessage in response.Messages)
            {
                try
                {
                    var eventType = sqsMessage.MessageAttributes.TryGetValue("eventType", out var attr)
                        ? attr.StringValue
                        : "Unknown";

                    // SQS messages forwarded from SNS wrap the payload in a JSON envelope
                    var payload = ExtractPayload(sqsMessage.Body);

                    var busMessage = new BusMessage(sqsMessage.MessageId, eventType, payload);
                    await handler(busMessage, cancellationToken);

                    await _sqs.DeleteMessageAsync(_settings.QueueUrl, sqsMessage.ReceiptHandle, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing SQS message {MessageId}.", sqsMessage.MessageId);
                }
            }
        }
    }

    private static string ExtractPayload(string sqsBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(sqsBody);
            if (doc.RootElement.TryGetProperty("Message", out var message))
                return message.GetString() ?? sqsBody;
        }
        catch
        {
            // Not an SNS-wrapped message — return as-is
        }

        return sqsBody;
    }
}
