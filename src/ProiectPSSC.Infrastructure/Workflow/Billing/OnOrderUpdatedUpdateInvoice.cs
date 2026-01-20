using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProiectPSSC.Application.Messaging;
using ProiectPSSC.Domain.Billing;
using ProiectPSSC.Domain.Events;
using ProiectPSSC.Infrastructure.Persistence;

namespace ProiectPSSC.Infrastructure.Workflow.Billing;

/// <summary>
/// When an order is updated, regenerate the invoice lines to match the new order lines.
/// This approach assumes simple invoice synchronization (no credit notes).
/// </summary>
public sealed class OnOrderUpdatedUpdateInvoice : IEventHandler<OrderUpdated>
{
    private readonly AppDbContext _db;
    private readonly ILogger<OnOrderUpdatedUpdateInvoice> _logger;

    public OnOrderUpdatedUpdateInvoice(AppDbContext db, ILogger<OnOrderUpdatedUpdateInvoice> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(OrderUpdated ev, CancellationToken ct)
    {
        // Find the invoice for this order
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.OrderId == ev.OrderId, ct);

        if (invoice is null)
        {
            _logger.LogWarning("No invoice found for order {OrderId} - skipping update", ev.OrderId.Value);
            return;
        }

        // Get the updated order lines
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ev.OrderId, ct);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found - skipping invoice update", ev.OrderId.Value);
            return;
        }

        // Convert order lines to invoice lines
        var newInvoiceLines = order.Lines
            .Select(l => new InvoiceLine(l.ProductCode, l.Quantity, l.UnitPrice))
            .ToList();

        var updateResult = invoice.UpdateLines(newInvoiceLines);
        if (!updateResult.IsSuccess)
        {
            _logger.LogWarning(
                "Cannot update invoice {InvoiceId} for order {OrderId}: {Error}",
                invoice.Id,
                ev.OrderId.Value,
                updateResult.Error?.Message);
            return;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated invoice {InvoiceId} for order {OrderId}: old total = {OldTotal}, new total = {NewTotal}",
            invoice.Id,
            ev.OrderId.Value,
            ev.OldTotal,
            ev.NewTotal);
    }
}
