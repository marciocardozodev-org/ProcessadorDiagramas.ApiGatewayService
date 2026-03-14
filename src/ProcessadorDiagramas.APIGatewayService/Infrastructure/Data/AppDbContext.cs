using Microsoft.EntityFrameworkCore;
using ProcessadorDiagramas.APIGatewayService.Domain.Entities;
using ProcessadorDiagramas.APIGatewayService.Inbox;
using ProcessadorDiagramas.APIGatewayService.Outbox;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DiagramRequest> DiagramRequests => Set<DiagramRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiagramRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FileName).HasMaxLength(260);
            entity.Property(e => e.ContentType).HasMaxLength(150);
            entity.Property(e => e.StoragePath).HasMaxLength(2048);
            entity.Property(e => e.DiagramContent).IsRequired().HasMaxLength(10000);
            entity.Property(e => e.Format).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.ReportUrl).HasMaxLength(2048);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Payload).IsRequired();
            entity.HasIndex(e => e.ProcessedAt);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Payload).IsRequired();
            entity.HasIndex(e => e.MessageId).IsUnique();
        });
    }
}
