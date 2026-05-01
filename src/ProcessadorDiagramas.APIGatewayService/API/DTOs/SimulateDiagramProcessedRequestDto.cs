using System.ComponentModel.DataAnnotations;

namespace ProcessadorDiagramas.APIGatewayService.API.DTOs;

public sealed class SimulateDiagramProcessedRequestDto
{
    [Required]
    public Guid DiagramRequestId { get; init; }

    public bool IsSuccess { get; init; }

    [MaxLength(2048)]
    public string? ResultUrl { get; init; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; init; }
}