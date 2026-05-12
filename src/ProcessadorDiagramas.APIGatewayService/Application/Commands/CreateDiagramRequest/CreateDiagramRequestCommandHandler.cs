using Microsoft.Extensions.Logging;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Contracts.Events;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Data;
using ProcessadorDiagramas.APIGatewayService.Outbox;

namespace ProcessadorDiagramas.APIGatewayService.Application.Commands.CreateDiagramRequest;

public sealed class CreateDiagramRequestCommandHandler
{
    private readonly IDiagramRequestRepository _repository;
    private readonly OutboxPublisher _outboxPublisher;
    private readonly AppDbContext _dbContext;
    private readonly IUploadOrquestracaoServiceClient _orchestrationClient;
    private readonly ILogger<CreateDiagramRequestCommandHandler> _logger;

    public CreateDiagramRequestCommandHandler(
        IDiagramRequestRepository repository,
        OutboxPublisher outboxPublisher,
        AppDbContext dbContext,
        IUploadOrquestracaoServiceClient orchestrationClient,
        ILogger<CreateDiagramRequestCommandHandler> logger)
    {
        _repository = repository;
        _outboxPublisher = outboxPublisher;
        _dbContext = dbContext;
        _orchestrationClient = orchestrationClient;
        _logger = logger;
    }

    public async Task<CreateDiagramRequestResponse> HandleAsync(
        CreateDiagramRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        var request = DiagramRequest.CreateFromUpload(
            command.StoragePath,
            command.FileName,
            command.FileSize,
            command.ContentType,
            command.Name,
            command.Description);

        await _repository.AddAsync(request, cancellationToken);

        var @event = new DiagramRequestCreatedEvent(
            request.Id,
            request.StoragePath ?? command.StoragePath,
            request.FileName ?? command.FileName,
            request.FileSize ?? command.FileSize,
            request.ContentType ?? command.ContentType,
            request.Name,
            request.Description,
            request.CreatedAt);

        await _outboxPublisher.PublishAsync(@event, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Attempt to register synchronously with UploadOrquestracaoService.
        // Failure is non-fatal: the event is already queued via Outbox for reliable delivery.
        var registerRequest = new RegisterUploadRequest(
            request.Id,
            request.StoragePath ?? command.StoragePath,
            request.FileName ?? command.FileName,
            request.FileSize ?? command.FileSize,
            request.ContentType ?? command.ContentType,
            request.Name,
            request.Description,
            request.CreatedAt);

        var orchestrationResult = await _orchestrationClient.RegisterUploadAsync(registerRequest, cancellationToken);
        if (orchestrationResult is null)
        {
            _logger.LogDebug(
                "UploadOrquestracaoService did not confirm registration for {DiagramRequestId}. " +
                "Event is queued via Outbox for reliable delivery.",
                request.Id);
        }

        return new CreateDiagramRequestResponse(
            request.Id,
            request.Status.ToString(),
            request.CreatedAt);
    }
}
