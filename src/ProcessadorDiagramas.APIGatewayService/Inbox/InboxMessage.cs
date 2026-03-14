namespace ProcessadorDiagramas.APIGatewayService.Inbox;

public sealed class InboxMessage
{
    public Guid Id { get; private set; }
    public string MessageId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime ReceivedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public bool IsProcessed { get; private set; }

    // Parameterless constructor for EF Core
    private InboxMessage() { }

    public static InboxMessage Create(string messageId, string eventType, string payload)
    {
        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            EventType = eventType,
            Payload = payload,
            ReceivedAt = DateTime.UtcNow,
            IsProcessed = false
        };
    }

    public void MarkAsProcessed()
    {
        IsProcessed = true;
        ProcessedAt = DateTime.UtcNow;
    }
}
