using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ProcessadorDiagramas.APIGatewayService.Outbox;

public sealed class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    public OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingMessages(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessages(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var pendingMessages = await outboxRepo.GetPendingAsync(stoppingToken);

        foreach (var msg in pendingMessages)
        {
            try
            {
                await messageBus.PublishAsync(msg.EventType, msg.Payload, stoppingToken);
                msg.MarkAsProcessed();
                _logger.LogInformation("Outbox message {Id} ({EventType}) published.", msg.Id, msg.EventType);
            }
            catch (Exception ex)
            {
                msg.MarkAsFailed(ex.Message);
                _logger.LogError(ex, "Failed to publish outbox message {Id}.", msg.Id);
            }
        }

        await outboxRepo.SaveChangesAsync(stoppingToken);
    }
}
