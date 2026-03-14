using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.APIGatewayService.Application.Interfaces;
using ProcessadorDiagramas.APIGatewayService.Inbox;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Data.Repositories;

public sealed class InboxRepository : IInboxRepository
{
    private readonly AppDbContext _context;

    public InboxRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default)
        => await _context.InboxMessages.AnyAsync(m => m.MessageId == messageId, cancellationToken);

    public async Task AddAsync(InboxMessage message, CancellationToken cancellationToken = default)
        => await _context.InboxMessages.AddAsync(message, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
