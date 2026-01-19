using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Domain.Events;

public sealed record OrderPlaced(
    Guid EventId,
    DateTimeOffset OccurredAt,
    OrderId OrderId,
    string CustomerEmail,
    decimal Total
);
