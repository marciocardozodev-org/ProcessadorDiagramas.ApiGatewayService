namespace ProcessadorDiagramas.APIGatewayService.Contracts.Events;

public record DiagramProcessedEvent(
    Guid DiagramRequestId,
    bool IsSuccess,
    string? ResultUrl,
    string? ErrorMessage,
    DateTime ProcessedAt);
