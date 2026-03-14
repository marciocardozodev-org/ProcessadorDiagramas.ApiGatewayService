namespace ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

public record StoredDiagramFile(
    string StoragePath,
    string FileName,
    long FileSize,
    string ContentType);

public interface IDiagramFileStorage
{
    Task<StoredDiagramFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
