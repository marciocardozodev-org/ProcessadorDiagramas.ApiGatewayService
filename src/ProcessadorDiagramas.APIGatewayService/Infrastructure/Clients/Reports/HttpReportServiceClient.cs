using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Clients.Reports;

public sealed class HttpReportServiceClient : IReportServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ReportServiceSettings _settings;
    private readonly ILogger<HttpReportServiceClient> _logger;

    public HttpReportServiceClient(
        HttpClient httpClient,
        IOptions<ReportServiceSettings> settings,
        ILogger<HttpReportServiceClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TechnicalReportDto?> GetReportAsync(Guid analysisId, CancellationToken cancellationToken = default)
    {
        var endpoint = _settings.GetReportPathTemplate.Replace("{analysisId}", analysisId.ToString());

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(endpoint, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Report service call failed for analysis {AnalysisId}.", analysisId);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Report service returned status code {StatusCode} for analysis {AnalysisId}.",
                response.StatusCode,
                analysisId);
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<ReportServicePayload>(cancellationToken: cancellationToken);
        if (payload is null)
            return null;

        return new TechnicalReportDto(
            analysisId,
            payload.Summary ?? "Technical report",
            payload.Details ?? string.Empty,
            payload.GeneratedAt ?? DateTime.UtcNow,
            payload.ReportUrl);
    }

    private sealed class ReportServicePayload
    {
        public string? Summary { get; set; }
        public string? Details { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public string? ReportUrl { get; set; }
    }
}
