using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Clients.UploadOrquestracao;

/// <summary>
/// HTTP client that delegates upload registration and status queries
/// to the UploadOrquestracaoService API.
/// </summary>
public sealed class HttpUploadOrquestracaoServiceClient : IUploadOrquestracaoServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly UploadOrquestracaoServiceSettings _settings;
    private readonly ILogger<HttpUploadOrquestracaoServiceClient> _logger;

    public HttpUploadOrquestracaoServiceClient(
        HttpClient httpClient,
        IOptions<UploadOrquestracaoServiceSettings> settings,
        ILogger<HttpUploadOrquestracaoServiceClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<OrchestrationProcessDto?> RegisterUploadAsync(
        RegisterUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(_settings.RegisterUploadPath, request, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "UploadOrquestracaoService unavailable while registering upload {DiagramRequestId}.", request.DiagramRequestId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "UploadOrquestracaoService returned {StatusCode} for RegisterUpload {DiagramRequestId}.",
                response.StatusCode,
                request.DiagramRequestId);
            return null;
        }

        return await DeserializeAsync(response, request.DiagramRequestId.ToString(), cancellationToken);
    }

    public async Task<OrchestrationProcessDto?> GetProcessStatusAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var path = _settings.GetStatusPathTemplate.Replace("{processId}", processId.ToString());

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(path, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "UploadOrquestracaoService unavailable while fetching status for {ProcessId}.", processId);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "UploadOrquestracaoService returned {StatusCode} for GetProcessStatus {ProcessId}.",
                response.StatusCode,
                processId);
            return null;
        }

        return await DeserializeAsync(response, processId.ToString(), cancellationToken);
    }

    private async Task<OrchestrationProcessDto?> DeserializeAsync(
        HttpResponseMessage response,
        string context,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<OrchestrationApiResponse>(cancellationToken: cancellationToken);
            if (payload is null)
                return null;

            return new OrchestrationProcessDto(
                payload.ProcessId,
                payload.Status ?? "Unknown",
                payload.CreatedAt ?? DateTime.UtcNow,
                payload.UpdatedAt,
                payload.ReportUrl,
                payload.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize UploadOrquestracaoService response for {Context}.", context);
            return null;
        }
    }

    // Mirrors the expected JSON shape from UploadOrquestracaoService
    private sealed class OrchestrationApiResponse
    {
        public Guid ProcessId { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? ReportUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
