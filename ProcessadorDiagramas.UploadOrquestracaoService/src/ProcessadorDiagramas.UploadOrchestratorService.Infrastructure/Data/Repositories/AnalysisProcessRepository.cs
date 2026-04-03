using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.UploadOrchestratorService.Application.Interfaces;
using ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;

namespace ProcessadorDiagramas.UploadOrchestratorService.Infrastructure.Data.Repositories;

public sealed class AnalysisProcessRepository : IAnalysisProcessRepository
{
    private readonly AppDbContext _dbContext;

    public AnalysisProcessRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AnalysisProcess process, CancellationToken cancellationToken)
    {
        await _dbContext.AnalysisProcesses.AddAsync(process, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnalysisProcess?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.AnalysisProcesses
            .Include("_statusHistory")
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(AnalysisProcess process, CancellationToken cancellationToken)
    {
        _dbContext.AnalysisProcesses.Update(process);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
