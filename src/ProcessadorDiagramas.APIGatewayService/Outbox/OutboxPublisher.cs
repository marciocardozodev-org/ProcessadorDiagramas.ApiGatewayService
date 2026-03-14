using System.Text.Json;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Outbox;

public sealed class OutboxPublisher
{
    private readonly IOutboxRepository _outboxRepository;

    public OutboxPublisher(IOutboxRepository outboxRepository)
    {
        _outboxRepository = outboxRepository;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        var eventType = typeof(T).Name;
        var payload = JsonSerializer.Serialize(@event);
        var message = OutboxMessage.Create(eventType, payload);
        await _outboxRepository.AddAsync(message, cancellationToken);
    }
}
