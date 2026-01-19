using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Domain.Events;

public sealed record OrderCancelled(
    Guid EventId,
    DateTimeOffset OccurredAt,
    OrderId OrderId,
    string Reason
);

