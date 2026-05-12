using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;

public sealed class GetDiagramRequestQueryHandler
{
    private readonly IDiagramRequestRepository _repository;
    private readonly IUploadOrquestracaoServiceClient _orchestrationClient;

    public GetDiagramRequestQueryHandler(
        IDiagramRequestRepository repository,
        IUploadOrquestracaoServiceClient orchestrationClient)
    {
        _repository = repository;
        _orchestrationClient = orchestrationClient;
    }

    public async Task<AnalysisStatusResponse?> HandleAsync(
        GetDiagramRequestQuery query,
        CancellationToken cancellationToken = default)
    {
        // Prefer the authoritative status from UploadOrquestracaoService.
        // Fall back to local DB when the service is unavailable (mock/dev/offline).
        var orchestrationStatus = await _orchestrationClient.GetProcessStatusAsync(query.Id, cancellationToken);
        if (orchestrationStatus is not null)
        {
            // Resolve the diagram format from the local DB (not tracked by orchestration service).
            var localRequest = await _repository.GetByIdAsync(query.Id, cancellationToken);
            var format = localRequest?.Format.ToString() ?? "Unknown";

            return new AnalysisStatusResponse(
                orchestrationStatus.ProcessId,
                format,
                orchestrationStatus.Status,
                orchestrationStatus.CreatedAt,
                orchestrationStatus.UpdatedAt,
                orchestrationStatus.ReportUrl,
                orchestrationStatus.ErrorMessage);
        }

        // Fallback: local DB (used in mock/dev mode and when orchestration service is down).
        var request = await _repository.GetByIdAsync(query.Id, cancellationToken);

        if (request is null)
            return null;

        return new AnalysisStatusResponse(
            request.Id,
            request.Format.ToString(),
            request.Status.ToString(),
            request.CreatedAt,
            request.UpdatedAt,
            request.ReportUrl,
            request.ErrorMessage);
    }
}
