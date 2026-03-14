using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.EventHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ProcessadorDiagramas.APIGatewayService.Inbox;

public sealed class InboxConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InboxConsumer> _logger;

    public InboxConsumer(IServiceScopeFactory scopeFactory, ILogger<InboxConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await messageBus.SubscribeAsync(
            async (msg, ct) => await HandleMessageAsync(msg, ct),
            stoppingToken);
    }

    private async Task HandleMessageAsync(BusMessage busMessage, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var inboxRepo = scope.ServiceProvider.GetRequiredService<IInboxRepository>();

        if (await inboxRepo.ExistsAsync(busMessage.MessageId, ct))
        {
            _logger.LogInformation("Duplicate inbox message {MessageId} skipped.", busMessage.MessageId);
            return;
        }

        var inboxMessage = InboxMessage.Create(busMessage.MessageId, busMessage.EventType, busMessage.Payload);
        await inboxRepo.AddAsync(inboxMessage, ct);

        var handlers = scope.ServiceProvider.GetServices<IEventHandler>();
        var handler = handlers.FirstOrDefault(h => h.EventType == busMessage.EventType);

        if (handler is not null)
            await handler.HandleAsync(busMessage.Payload, ct);
        else
            _logger.LogWarning("No handler registered for event type '{EventType}'.", busMessage.EventType);

        inboxMessage.MarkAsProcessed();
        await inboxRepo.SaveChangesAsync(ct);
    }
}
