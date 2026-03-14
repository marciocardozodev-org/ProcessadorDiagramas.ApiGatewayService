using ProcessadorDiagramas.APIGatewayService.Inbox;

namespace ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

public interface IInboxRepository
{
    Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default);
    Task AddAsync(InboxMessage message, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
