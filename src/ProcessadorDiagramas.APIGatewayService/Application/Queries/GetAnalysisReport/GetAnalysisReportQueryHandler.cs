using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Domain.Enums;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Application.Queries.GetAnalysisReport;

public sealed class GetAnalysisReportQueryHandler
{
    private readonly IDiagramRequestRepository _repository;
    private readonly IReportServiceClient _reportServiceClient;

    public GetAnalysisReportQueryHandler(
        IDiagramRequestRepository repository,
        IReportServiceClient reportServiceClient)
    {
        _repository = repository;
        _reportServiceClient = reportServiceClient;
    }

    public async Task<GetAnalysisReportResult> HandleAsync(
        GetAnalysisReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var request = await _repository.GetByIdAsync(query.AnalysisId, cancellationToken);

        if (request is null)
            return GetAnalysisReportResult.NotFound(query.AnalysisId);

        if (request.Status != DiagramStatus.Analyzed)
            return GetAnalysisReportResult.NotReady(request.Id, request.Status.ToString());

        var externalReport = await _reportServiceClient.GetReportAsync(request.Id, cancellationToken);

        var response = externalReport is null
            ? new AnalysisReportResponse(
                request.Id,
                request.Status.ToString(),
                request.ReportUrl,
                null,
                null,
                request.UpdatedAt)
            : new AnalysisReportResponse(
                request.Id,
                request.Status.ToString(),
                externalReport.ReportUrl ?? request.ReportUrl,
                externalReport.Summary,
                externalReport.Details,
                externalReport.GeneratedAt);

        return GetAnalysisReportResult.Ready(response);
    }
}
