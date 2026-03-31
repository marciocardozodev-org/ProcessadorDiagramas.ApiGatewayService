using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProcessadorDiagramas.APIGatewayService.API.DTOs;

public sealed class CreateDiagramRequestDto
{
    /// <summary>
    /// Diagram file to be processed. Supported types: image/* and application/pdf. Maximum size: 10MB.
    /// </summary>
    [Required]
    public IFormFile? File { get; init; }

    /// <summary>
    /// Optional display name for the uploaded diagram.
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; init; }

    /// <summary>
    /// Optional description with additional context for the analysis.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; init; }
}
