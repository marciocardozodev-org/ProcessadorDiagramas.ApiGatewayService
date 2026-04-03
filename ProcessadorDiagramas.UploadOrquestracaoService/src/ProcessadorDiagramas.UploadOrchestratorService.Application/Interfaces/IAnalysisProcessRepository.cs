using ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;

namespace ProcessadorDiagramas.UploadOrchestratorService.Application.Interfaces;

public interface IAnalysisProcessRepository
{
    Task AddAsync(AnalysisProcess process, CancellationToken cancellationToken);
    Task<AnalysisProcess?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(AnalysisProcess process, CancellationToken cancellationToken);
}
