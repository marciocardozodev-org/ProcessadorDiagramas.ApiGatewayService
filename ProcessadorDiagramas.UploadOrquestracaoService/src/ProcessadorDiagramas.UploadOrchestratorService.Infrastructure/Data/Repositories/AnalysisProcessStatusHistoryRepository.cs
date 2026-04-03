using ProcessadorDiagramas.UploadOrchestratorService.Application.Interfaces;
using ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;

namespace ProcessadorDiagramas.UploadOrchestratorService.Infrastructure.Data.Repositories;

public sealed class AnalysisProcessStatusHistoryRepository : IAnalysisProcessStatusHistoryRepository
{
    private readonly AppDbContext _dbContext;

    public AnalysisProcessStatusHistoryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<AnalysisProcessStatusHistory> statusEntries,
        CancellationToken cancellationToken)
    {
        await _dbContext.AnalysisProcessStatusHistories.AddRangeAsync(statusEntries, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
