using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Clients.UploadOrquestracao;

/// <summary>
/// No-op client used in development and tests when UploadOrquestracaoService is unavailable.
/// Returning null signals the caller to fall back to the local database.
/// </summary>
public sealed class MockUploadOrquestracaoServiceClient : IUploadOrquestracaoServiceClient
{
    private readonly ILogger<MockUploadOrquestracaoServiceClient> _logger;

    public MockUploadOrquestracaoServiceClient(ILogger<MockUploadOrquestracaoServiceClient> logger)
    {
        _logger = logger;
    }

    public Task<OrchestrationProcessDto?> RegisterUploadAsync(
        RegisterUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[Mock] RegisterUpload skipped for {DiagramRequestId}. Event will be delivered via Outbox.",
            request.DiagramRequestId);
        return Task.FromResult<OrchestrationProcessDto?>(null);
    }

    public Task<OrchestrationProcessDto?> GetProcessStatusAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[Mock] GetProcessStatus skipped for {ProcessId}. Using local DB.", processId);
        return Task.FromResult<OrchestrationProcessDto?>(null);
    }
}
