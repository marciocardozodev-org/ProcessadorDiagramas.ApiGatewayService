using ProcessadorDiagramas.UploadOrchestratorService.Domain.Enums;

namespace ProcessadorDiagramas.UploadOrchestratorService.Application.AnalysisProcesses;

public sealed record AnalysisProcessStatusResult(
    Guid Id,
    string OriginalFileName,
    string StoredFileKey,
    string ContentType,
    long FileSize,
    AnalysisProcessStatus Status,
    string CorrelationId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? FailureReason);
