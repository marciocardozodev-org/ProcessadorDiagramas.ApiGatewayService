using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Messaging;

/// <summary>
/// Dummy message bus implementation for local development and testing.
/// Doesn't actually send messages to AWS SQS/SNS.
/// </summary>
public class DummyMessageBus : IMessageBus
{
    private readonly ILogger<DummyMessageBus> _logger;

    public DummyMessageBus(ILogger<DummyMessageBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DummyMessageBus: Publishing event {EventType} (not sent to SNS in dev mode)",
            eventType);
        await Task.CompletedTask;
    }

    public async Task SubscribeAsync(Func<BusMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DummyMessageBus: Subscribed to messages (no actual SQS messages in dev mode)");
        await Task.CompletedTask;
    }
}
