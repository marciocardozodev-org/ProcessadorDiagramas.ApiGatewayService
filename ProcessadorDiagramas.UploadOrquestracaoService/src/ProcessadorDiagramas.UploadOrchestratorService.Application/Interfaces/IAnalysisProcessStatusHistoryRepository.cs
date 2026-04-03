using ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;

namespace ProcessadorDiagramas.UploadOrchestratorService.Application.Interfaces;

public interface IAnalysisProcessStatusHistoryRepository
{
    Task AddRangeAsync(
        IReadOnlyCollection<AnalysisProcessStatusHistory> statusEntries,
        CancellationToken cancellationToken);
}
