using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Messaging;
using ProiectPSSC.Domain.Billing;
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
        // Idempotency: don't create multiple invoices for the same order.
        var exists = await _db.Invoices
            .AsNoTracking()
            .AnyAsync(i => i.OrderId == ev.OrderId, ct);

        if (exists)
            return;

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == ev.OrderId, ct);

        if (order is null)
            return; // order missing; nothing to bill

        var invoiceId = Guid.NewGuid();
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{invoiceId.ToString()[..8].ToUpperInvariant()}";
        var dueDate = DateTimeOffset.UtcNow.AddDays(14);

        var lines = order.Lines.Select(l => new InvoiceLine(l.ProductCode, l.Quantity, l.UnitPrice)).ToList();

        var invoice = Invoice.Create(
            invoiceId: invoiceId,
            number: invoiceNumber,
            orderId: order.Id,
            billingEmail: order.CustomerEmail,
            currency: Currency.RON,
            dueDate: dueDate,
            lines: lines
        );

        _db.Invoices.Add(invoice);

        // Mark the order as invoiced
        order.MarkAsInvoiced();

        var invoiceCreated = new InvoiceCreated(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            InvoiceId: invoiceId,
            OrderId: order.Id,
            Amount: invoice.Amount
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
