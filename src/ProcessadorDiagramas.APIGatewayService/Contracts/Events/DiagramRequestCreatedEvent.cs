namespace ProcessadorDiagramas.APIGatewayService.Contracts.Events;

public record DiagramRequestCreatedEvent(
    Guid DiagramRequestId,
    string StoragePath,
    string FileName,
    long FileSize,
    string ContentType,
    string? Name,
    string? Description,
    DateTime CreatedAt);
