using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;

public sealed class GetDiagramRequestQueryHandler
{
    private readonly IDiagramRequestRepository _repository;

    public GetDiagramRequestQueryHandler(IDiagramRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<AnalysisStatusResponse?> HandleAsync(
        GetDiagramRequestQuery query,
        CancellationToken cancellationToken = default)
    {
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
