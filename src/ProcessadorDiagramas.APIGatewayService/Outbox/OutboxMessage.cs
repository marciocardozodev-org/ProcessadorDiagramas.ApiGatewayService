namespace ProcessadorDiagramas.APIGatewayService.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }

    // Parameterless constructor for EF Core
    private OutboxMessage() { }

    public static OutboxMessage Create(string eventType, string payload)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = payload,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
    }
}
