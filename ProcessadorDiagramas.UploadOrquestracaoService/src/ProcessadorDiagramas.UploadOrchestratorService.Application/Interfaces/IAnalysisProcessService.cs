using ProcessadorDiagramas.UploadOrchestratorService.Application.AnalysisProcesses;

namespace ProcessadorDiagramas.UploadOrchestratorService.Application.Interfaces;

public interface IAnalysisProcessService
{
    Task<AnalysisProcessStatusResult> CreateAsync(
        CreateAnalysisProcessRequest request,
        CancellationToken cancellationToken);

    Task<AnalysisProcessStatusResult?> GetByIdAsync(
        Guid processId,
        CancellationToken cancellationToken);
}
