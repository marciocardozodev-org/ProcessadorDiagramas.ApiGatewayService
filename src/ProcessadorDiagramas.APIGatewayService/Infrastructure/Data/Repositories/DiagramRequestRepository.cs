using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Domain.Interfaces;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Data.Repositories;

public sealed class DiagramRequestRepository : IDiagramRequestRepository
{
    private readonly AppDbContext _context;

    public DiagramRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DiagramRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.DiagramRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddAsync(DiagramRequest request, CancellationToken cancellationToken = default)
        => await _context.DiagramRequests.AddAsync(request, cancellationToken);

    public Task UpdateAsync(DiagramRequest request, CancellationToken cancellationToken = default)
    {
        _context.DiagramRequests.Update(request);
        return _context.SaveChangesAsync(cancellationToken);
    }
}
