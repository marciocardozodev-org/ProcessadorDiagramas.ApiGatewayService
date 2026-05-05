namespace ProcessadorDiagramas.APIGatewayService.Application.Commands.CreateDiagramRequest;
//teste deploy
public record CreateDiagramRequestCommand(
	string StoragePath,
	string FileName,
	long FileSize,
	string ContentType,
	string? Name,
	string? Description);
