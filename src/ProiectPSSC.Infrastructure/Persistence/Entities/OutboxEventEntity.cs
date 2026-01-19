namespace ProiectPSSC.Infrastructure.Persistence.Entities;

public sealed class OutboxEventEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
}
