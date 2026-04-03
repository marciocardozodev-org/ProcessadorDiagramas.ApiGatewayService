namespace ProcessadorDiagramas.UploadOrchestratorService.Application.AnalysisProcesses;

public sealed record CreateAnalysisProcessRequest(
    string OriginalFileName,
    string StoredFileKey,
    string ContentType,
    long FileSize,
    string CorrelationId);
