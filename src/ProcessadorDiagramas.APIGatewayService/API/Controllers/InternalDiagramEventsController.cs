using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcessadorDiagramas.APIGatewayService.API.DTOs;
using ProcessadorDiagramas.APIGatewayService.Contracts.Events;
using ProcessadorDiagramas.APIGatewayService.EventHandlers;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Auth;

namespace ProcessadorDiagramas.APIGatewayService.API.Controllers;

[ApiController]
[Route("internal/diagram-events")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.InternalAccess)]
public sealed class InternalDiagramEventsController : ControllerBase
{
    private readonly DiagramProcessedEventHandler _diagramProcessedEventHandler;

    public InternalDiagramEventsController(DiagramProcessedEventHandler diagramProcessedEventHandler)
    {
        _diagramProcessedEventHandler = diagramProcessedEventHandler;
    }

    [HttpPost("processed")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> DiagramProcessed(
        [FromBody] SimulateDiagramProcessedRequestDto dto,
        CancellationToken cancellationToken)
    {
        var processedEvent = new DiagramProcessedEvent(
            dto.DiagramRequestId,
            dto.IsSuccess,
            dto.ResultUrl,
            dto.ErrorMessage,
            dto.ProcessedAt ?? DateTime.UtcNow);

        var payload = JsonSerializer.Serialize(processedEvent);
        await _diagramProcessedEventHandler.HandleAsync(payload, cancellationToken);

        return Accepted(new
        {
            dto.DiagramRequestId,
            dto.IsSuccess,
            ProcessedAt = processedEvent.ProcessedAt
        });
    }
}