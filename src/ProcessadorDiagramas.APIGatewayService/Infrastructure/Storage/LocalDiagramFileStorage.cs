using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Storage;

public sealed class LocalDiagramFileStorage : IDiagramFileStorage
{
    private readonly UploadStorageSettings _settings;
    private readonly ILogger<LocalDiagramFileStorage> _logger;

    public LocalDiagramFileStorage(
        IOptions<UploadStorageSettings> settings,
        ILogger<LocalDiagramFileStorage> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<StoredDiagramFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));

        var uploadsRoot = string.IsNullOrWhiteSpace(_settings.RootPath)
            ? "/tmp/uploads"
            : _settings.RootPath;

        Directory.CreateDirectory(uploadsRoot);

        var originalFileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(uploadsRoot, storedFileName);

        await using (var target = File.Create(storedPath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var storedInfo = new FileInfo(storedPath);

        _logger.LogInformation(
            "File uploaded to local storage at {StoragePath}. FileName: {FileName}. Size: {FileSize}.",
            storedPath,
            originalFileName,
            storedInfo.Length);

        return new StoredDiagramFile(
            storedPath,
            originalFileName,
            storedInfo.Length,
            contentType);
    }
}
