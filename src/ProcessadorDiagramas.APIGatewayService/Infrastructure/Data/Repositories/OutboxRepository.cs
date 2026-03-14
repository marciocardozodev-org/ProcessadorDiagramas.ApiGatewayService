using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Outbox;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Data.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _context;

    public OutboxRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        => await _context.OutboxMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(CancellationToken cancellationToken = default)
        => await _context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Error == null)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
