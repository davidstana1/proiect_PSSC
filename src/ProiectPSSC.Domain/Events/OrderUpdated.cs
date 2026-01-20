using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Domain.Events;

public sealed record OrderUpdated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    OrderId OrderId,
    string CustomerEmail,
    decimal OldTotal,
    decimal NewTotal
);
