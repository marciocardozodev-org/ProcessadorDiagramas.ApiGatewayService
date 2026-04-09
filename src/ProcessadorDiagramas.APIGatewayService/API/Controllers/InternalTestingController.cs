using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcessadorDiagramas.APIGatewayService.API.DTOs;
using ProcessadorDiagramas.APIGatewayService.Contracts.Events;
using ProcessadorDiagramas.APIGatewayService.EventHandlers;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Auth;

namespace ProcessadorDiagramas.APIGatewayService.API.Controllers;

[ApiController]
[Route("internal/testing")]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = AuthorizationPolicies.InternalAccess)]
public sealed class InternalTestingController : ControllerBase
{
    private readonly DiagramProcessedEventHandler _diagramProcessedEventHandler;
    private readonly IWebHostEnvironment _environment;

    public InternalTestingController(
        DiagramProcessedEventHandler diagramProcessedEventHandler,
        IWebHostEnvironment environment)
    {
        _diagramProcessedEventHandler = diagramProcessedEventHandler;
        _environment = environment;
    }

    [HttpPost("diagram-processed")]
    public async Task<IActionResult> SimulateDiagramProcessed(
        [FromBody] SimulateDiagramProcessedRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var processedEvent = new DiagramProcessedEvent(
            dto.DiagramRequestId,
            dto.IsSuccess,
            dto.ResultUrl,
            dto.ErrorMessage,
            DateTime.UtcNow);

        var payload = JsonSerializer.Serialize(processedEvent);
        await _diagramProcessedEventHandler.HandleAsync(payload, cancellationToken);

        return Accepted(new { dto.DiagramRequestId, dto.IsSuccess });
    }
}