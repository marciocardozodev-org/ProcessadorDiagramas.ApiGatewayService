namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Clients.Reports;

public sealed class ReportServiceSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8081";
    public string GetReportPathTemplate { get; set; } = "/api/reports/{analysisId}";
    public bool UseMock { get; set; }
}
