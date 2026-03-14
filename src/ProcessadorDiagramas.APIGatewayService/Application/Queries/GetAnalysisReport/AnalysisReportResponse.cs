namespace ProcessadorDiagramas.APIGatewayService.Application.Queries.GetAnalysisReport;

public record AnalysisReportResponse(
    Guid Id,
    string Status,
    string? ReportUrl,
    string? Summary,
    string? Details,
    DateTime? GeneratedAt);
