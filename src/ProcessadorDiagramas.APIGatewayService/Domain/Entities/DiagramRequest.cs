using ProcessadorDiagramas.APIGatewayService.Domain.Enums;

namespace ProcessadorDiagramas.APIGatewayService.Domain.Entities;

public sealed class DiagramRequest
{
    public Guid Id { get; private set; }
    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public string? FileName { get; private set; }
    public long? FileSize { get; private set; }
    public string? ContentType { get; private set; }
    public string? StoragePath { get; private set; }
    public string DiagramContent { get; private set; } = string.Empty;
    public DiagramFormat Format { get; private set; }
    public DiagramStatus Status { get; private set; }
    public string? ReportUrl { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Parameterless constructor for EF Core
    private DiagramRequest() { }

    public static DiagramRequest Create(string diagramContent, DiagramFormat format)
    {
        if (string.IsNullOrWhiteSpace(diagramContent))
            throw new ArgumentException("Diagram content cannot be empty.", nameof(diagramContent));

        return new DiagramRequest
        {
            Id = Guid.NewGuid(),
            DiagramContent = diagramContent,
            Format = format,
            Status = DiagramStatus.Received,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static DiagramRequest CreateFromUpload(
        string storagePath,
        string fileName,
        long fileSize,
        string contentType,
        string? name,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be empty.", nameof(storagePath));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));

        if (fileSize <= 0)
            throw new ArgumentException("File size must be greater than zero.", nameof(fileSize));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));

        return new DiagramRequest
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            FileName = fileName,
            FileSize = fileSize,
            ContentType = contentType,
            StoragePath = storagePath,
            DiagramContent = storagePath,
            Format = ResolveFormat(contentType),
            Status = DiagramStatus.Received,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static DiagramFormat ResolveFormat(string contentType)
    {
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return DiagramFormat.DrawIo;

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return DiagramFormat.DrawIo;

        return DiagramFormat.DrawIo;
    }

    public void MarkAsProcessing()
    {
        if (Status != DiagramStatus.Received)
            throw new InvalidOperationException($"Cannot transition to Processing from status {Status}.");

        Status = DiagramStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsAnalyzed(string? reportUrl = null)
    {
        if (Status != DiagramStatus.Processing)
            throw new InvalidOperationException($"Cannot transition to Analyzed from status {Status}.");

        Status = DiagramStatus.Analyzed;
        ReportUrl = reportUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsError(string? errorMessage = null)
    {
        Status = DiagramStatus.Error;
        ErrorMessage = errorMessage;
        UpdatedAt = DateTime.UtcNow;
    }
}
