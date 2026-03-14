namespace ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;

/// <summary>Status snapshot returned by GET /api/diagrams/{id}.</summary>
public record AnalysisStatusResponse(
    Guid Id,
    string Format,
    string Status,
    DateTime ReceivedAt,
    DateTime? LastUpdatedAt,
    string? ReportUrl,
    string? ErrorMessage);
