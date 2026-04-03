using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProcessadorDiagramas.UploadOrchestratorService.Domain.Entities;

namespace ProcessadorDiagramas.UploadOrchestratorService.Infrastructure.Data.Configurations;

public sealed class AnalysisProcessStatusHistoryConfiguration : IEntityTypeConfiguration<AnalysisProcessStatusHistory>
{
    public void Configure(EntityTypeBuilder<AnalysisProcessStatusHistory> builder)
    {
        builder.ToTable("analysis_process_status_history");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ChangedAtUtc)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(2048);

        builder.HasIndex(x => new { x.AnalysisProcessId, x.ChangedAtUtc });
    }
}
