namespace ProcessadorDiagramas.APIGatewayService.Application.Queries.GetAnalysisReport;

public enum GetAnalysisReportOutcome
{
    NotFound = 1,
    NotReady = 2,
    Ready = 3
}

public record GetAnalysisReportResult(
    GetAnalysisReportOutcome Outcome,
    AnalysisReportResponse? Report,
    string? Message)
{
    public static GetAnalysisReportResult NotFound(Guid id) =>
        new(GetAnalysisReportOutcome.NotFound, null, $"Analysis '{id}' was not found.");

    public static GetAnalysisReportResult NotReady(Guid id, string currentStatus) =>
        new(
            GetAnalysisReportOutcome.NotReady,
            null,
            $"Analysis '{id}' is currently '{currentStatus}' and report is not available yet.");

    public static GetAnalysisReportResult Ready(AnalysisReportResponse report) =>
        new(GetAnalysisReportOutcome.Ready, report, null);
}
