using System.Text.Json;
using ProcessadorDiagramas.APIGatewayService.Contracts.Events;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ProcessadorDiagramas.APIGatewayService.EventHandlers;

public sealed class DiagramProcessedEventHandler : IEventHandler
{
    public string EventType => nameof(DiagramProcessedEvent);

    private readonly IDiagramRequestRepository _repository;
    private readonly ILogger<DiagramProcessedEventHandler> _logger;

    public DiagramProcessedEventHandler(
        IDiagramRequestRepository repository,
        ILogger<DiagramProcessedEventHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        DiagramProcessedEvent? @event;
        try
        {
            @event = JsonSerializer.Deserialize<DiagramProcessedEvent>(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not deserialize DiagramProcessedEvent payload.");
            return;
        }

        if (@event is null)
        {
            _logger.LogWarning("Could not deserialize DiagramProcessedEvent payload.");
            return;
        }

        var request = await _repository.GetByIdAsync(@event.DiagramRequestId, cancellationToken);

        if (request is null)
        {
            _logger.LogWarning("DiagramRequest {Id} not found when handling DiagramProcessedEvent.", @event.DiagramRequestId);
            return;
        }

        if (request.Status == Domain.Enums.DiagramStatus.Received)
            request.MarkAsProcessing();

        if (@event.IsSuccess)
            request.MarkAsAnalyzed(@event.ResultUrl);
        else
            request.MarkAsError(@event.ErrorMessage);

        await _repository.UpdateAsync(request, cancellationToken);

        _logger.LogInformation(
            "DiagramRequest {Id} marked as {Status}.",
            request.Id,
            request.Status);
    }
}
