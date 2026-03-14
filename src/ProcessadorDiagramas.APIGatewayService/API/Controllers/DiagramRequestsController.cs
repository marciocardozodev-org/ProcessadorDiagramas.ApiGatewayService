using Microsoft.AspNetCore.Mvc;
using ProcessadorDiagramas.APIGatewayService.API.DTOs;
using ProcessadorDiagramas.APIGatewayService.Application.Commands.CreateDiagramRequest;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetAnalysisReport;
using ProcessadorDiagramas.APIGatewayService.Application.Queries.GetDiagramRequest;

namespace ProcessadorDiagramas.APIGatewayService.API.Controllers;

[ApiController]
[Route("api/diagrams")]
[Produces("application/json")]
public sealed class DiagramRequestsController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly CreateDiagramRequestCommandHandler _createHandler;
    private readonly GetDiagramRequestQueryHandler _getHandler;
    private readonly GetAnalysisReportQueryHandler _getReportHandler;
    private readonly IDiagramFileStorage _fileStorage;

    public DiagramRequestsController(
        CreateDiagramRequestCommandHandler createHandler,
        GetDiagramRequestQueryHandler getHandler,
        GetAnalysisReportQueryHandler getReportHandler,
        IDiagramFileStorage fileStorage)
    {
        _createHandler = createHandler;
        _getHandler = getHandler;
        _getReportHandler = getReportHandler;
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// Submits a new diagram file for processing.
    /// </summary>
    /// <remarks>
    /// Example multipart request:
    ///
    ///     POST /api/diagrams
    ///     Content-Type: multipart/form-data
    ///     file: architecture.png (required, image/* or application/pdf, max 10MB)
    ///     name: Current architecture (optional)
    ///     description: Diagram for payment flow (optional)
    ///
    /// </remarks>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CreateDiagramRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromForm] CreateDiagramRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.File is null || dto.File.Length <= 0)
            return BadRequest(new { message = "Field 'file' is required." });

        if (dto.File.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "The uploaded file exceeds the maximum size of 10MB." });

        if (!IsSupportedContentType(dto.File.ContentType))
            return BadRequest(new { message = "Unsupported file type. Allowed types: image/* or application/pdf." });

        await using var stream = dto.File.OpenReadStream();

        var storedFile = await _fileStorage.SaveAsync(
            stream,
            dto.File.FileName,
            dto.File.ContentType,
            cancellationToken);

        var command = new CreateDiagramRequestCommand(
            storedFile.StoragePath,
            storedFile.FileName,
            storedFile.FileSize,
            storedFile.ContentType,
            dto.Name,
            dto.Description);

        var result = await _createHandler.HandleAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    private static bool IsSupportedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the status of an existing diagram request.
    /// </summary>
    /// <remarks>
    /// Example request:
    ///
    ///     GET /api/diagrams/{id}
    ///
    /// Example response:
    ///
    ///     {
    ///         "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "format": "PlantUML",
    ///         "status": "Processing",
    ///         "receivedAt": "2026-03-14T21:00:00Z",
    ///         "lastUpdatedAt": "2026-03-14T21:00:10Z",
    ///         "reportUrl": null,
    ///         "errorMessage": null
    ///     }
    ///
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnalysisStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _getHandler.HandleAsync(new GetDiagramRequestQuery(id), cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Returns the technical report of an analyzed request.
    /// </summary>
    /// <remarks>
    /// Example request:
    ///
    ///     GET /api/diagrams/{id}/report
    ///
    /// Example response:
    ///
    ///     {
    ///         "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "status": "Analyzed",
    ///         "reportUrl": "https://reports.local/analysis-123",
    ///         "summary": "Architecture findings",
    ///         "details": "Detected missing retry policy in integration flow.",
    ///         "generatedAt": "2026-03-14T21:02:00Z"
    ///     }
    ///
    /// </remarks>
    [HttpGet("{id:guid}/report")]
    [ProducesResponseType(typeof(AnalysisReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _getReportHandler.HandleAsync(new GetAnalysisReportQuery(id), cancellationToken);

        return result.Outcome switch
        {
            GetAnalysisReportOutcome.NotFound => NotFound(new { message = result.Message }),
            GetAnalysisReportOutcome.NotReady => Conflict(new { message = result.Message }),
            _ => Ok(result.Report)
        };
    }
}
