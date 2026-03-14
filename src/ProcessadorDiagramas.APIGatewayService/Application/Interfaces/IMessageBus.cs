namespace ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

public record BusMessage(string MessageId, string EventType, string Payload);

public interface IMessageBus
{
    Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default);
    Task SubscribeAsync(Func<BusMessage, CancellationToken, Task> handler, CancellationToken cancellationToken = default);
}
