using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ProcessadorDiagramas.APIGatewayService.API.DTOs;

public sealed class CreateDiagramRequestDto
{
    [Required]
    public IFormFile? File { get; init; }

    [MaxLength(200)]
    public string? Name { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }
}
