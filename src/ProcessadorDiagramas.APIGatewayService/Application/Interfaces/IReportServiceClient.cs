namespace ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

public record TechnicalReportDto(
    Guid AnalysisId,
    string Summary,
    string Details,
    DateTime GeneratedAt,
    string? ReportUrl);

public interface IReportServiceClient
{
    Task<TechnicalReportDto?> GetReportAsync(Guid analysisId, CancellationToken cancellationToken = default);
}
