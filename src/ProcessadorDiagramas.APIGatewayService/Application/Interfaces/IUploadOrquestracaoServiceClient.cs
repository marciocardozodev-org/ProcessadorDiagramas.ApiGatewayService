namespace ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

/// <summary>
/// Contract for consulting UploadOrquestracaoService.
/// The Gateway delegates process creation and status queries to this service,
/// which is the authoritative source of truth for the upload/analysis workflow.
/// </summary>
public interface IUploadOrquestracaoServiceClient
{
    /// <summary>
    /// Registers a new upload request with the orchestration service.
    /// Called after file storage so the orchestration service can start the workflow.
    /// </summary>
    Task<OrchestrationProcessDto?> RegisterUploadAsync(
        RegisterUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the current process status from the orchestration service.
    /// Returns null when the orchestration service is unavailable or process is not found.
    /// </summary>
    Task<OrchestrationProcessDto?> GetProcessStatusAsync(
        Guid processId,
        CancellationToken cancellationToken = default);
}

public record RegisterUploadRequest(
    Guid DiagramRequestId,
    string StoragePath,
    string FileName,
    long FileSize,
    string ContentType,
    string? Name,
    string? Description,
    DateTime CreatedAt);

public record OrchestrationProcessDto(
    Guid ProcessId,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? ReportUrl,
    string? ErrorMessage);
