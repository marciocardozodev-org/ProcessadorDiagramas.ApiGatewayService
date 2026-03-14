using ProcessadorDiagramas.APIGatewayService.Domain.Entities;

namespace ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;

public interface IDiagramRequestRepository
{
    Task<DiagramRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(DiagramRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(DiagramRequest request, CancellationToken cancellationToken = default);
}
