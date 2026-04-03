using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;
using ProcessadorDiagramas.UploadOrchestratorService.Infrastructure.Data.Configurations;

namespace ProcessadorDiagramas.UploadOrchestratorService.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AnalysisProcess> AnalysisProcesses => Set<AnalysisProcess>();
    public DbSet<AnalysisProcessStatusHistory> AnalysisProcessStatusHistories => Set<AnalysisProcessStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AnalysisProcessConfiguration());
        modelBuilder.ApplyConfiguration(new AnalysisProcessStatusHistoryConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
