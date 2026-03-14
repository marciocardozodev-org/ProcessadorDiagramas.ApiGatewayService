namespace ProcessadorDiagramas.APIGatewayService.EventHandlers;

public interface IEventHandler
{
    string EventType { get; }
    Task HandleAsync(string payload, CancellationToken cancellationToken = default);
}
