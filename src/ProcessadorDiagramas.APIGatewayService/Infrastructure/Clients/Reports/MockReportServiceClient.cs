using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Clients.Reports;

public sealed class MockReportServiceClient : IReportServiceClient
{
    private readonly ReportServiceSettings _settings;

    public MockReportServiceClient(IOptions<ReportServiceSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<TechnicalReportDto?> GetReportAsync(Guid analysisId, CancellationToken cancellationToken = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_settings.BaseUrl)
            ? "http://mock-report-service.local"
            : _settings.BaseUrl.TrimEnd('/');

        TechnicalReportDto report = new(
            analysisId,
            "Relatorio tecnico local",
            "Relatorio gerado pelo mock local do API Gateway para validacao de fluxo em desenvolvimento.",
            DateTime.UtcNow,
            $"{baseUrl}/api/reports/{analysisId}");

        return Task.FromResult<TechnicalReportDto?>(report);
    }
}