using ProcessadorDiagramas.APIGatewayService.Outbox;

namespace ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
