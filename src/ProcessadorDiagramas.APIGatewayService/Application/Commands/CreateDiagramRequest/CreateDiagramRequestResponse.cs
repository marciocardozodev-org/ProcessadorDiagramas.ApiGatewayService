namespace ProcessadorDiagramas.APIGatewayService.Application.Commands.CreateDiagramRequest;

public record CreateDiagramRequestResponse(Guid Id, string Status, DateTime CreatedAt);
