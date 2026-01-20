using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Domain.Events;

public sealed record OrderShipped(
    Guid EventId,
    DateTimeOffset OccurredAt,
    OrderId OrderId,
    Guid ShipmentId,
    string? TrackingNumber
);
