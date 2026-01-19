using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Messaging;
using ProiectPSSC.Domain.Events;
using ProiectPSSC.Infrastructure.Persistence;
using ProiectPSSC.Infrastructure.Persistence.Entities;

namespace ProiectPSSC.Infrastructure.Workflow.Billing;

public sealed class OnOrderPlacedCreateInvoice : IEventHandler<OrderPlaced>
{
    private readonly AppDbContext _db;

    public OnOrderPlacedCreateInvoice(AppDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(OrderPlaced ev, CancellationToken ct)
    {
        // minimal invoice generation: create invoice id and publish InvoiceCreated to outbox
        var invoiceId = Guid.NewGuid();

        var invoiceCreated = new InvoiceCreated(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            InvoiceId: invoiceId,
            OrderId: ev.OrderId,
            Amount: ev.Total
        );

        _db.OutboxEvents.Add(new OutboxEventEntity
        {
            Id = invoiceCreated.EventId,
            OccurredAt = invoiceCreated.OccurredAt,
            Type = nameof(InvoiceCreated),
            Payload = JsonSerializer.Serialize(invoiceCreated),
            Attempts = 0
        });

        await _db.SaveChangesAsync(ct);
    }
}
