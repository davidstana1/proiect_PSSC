using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Domain.Events;

public sealed record InvoiceCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid InvoiceId,
    OrderId OrderId,
    decimal Amount
);
