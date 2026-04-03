using ProcessadorDiagramas.UploadOrchestratorService.Domain.Enums;

namespace ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;

public sealed class AnalysisProcessStatusHistory
{
    private AnalysisProcessStatusHistory()
    {
    }

    public AnalysisProcessStatusHistory(
        Guid id,
        Guid analysisProcessId,
        AnalysisProcessStatus status,
        DateTime changedAtUtc,
        string? reason)
    {
        Id = id;
        AnalysisProcessId = analysisProcessId;
        Status = status;
        ChangedAtUtc = changedAtUtc;
        Reason = reason;
    }

    public Guid Id { get; private set; }
    public Guid AnalysisProcessId { get; private set; }
    public AnalysisProcessStatus Status { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Reason { get; private set; }
}
