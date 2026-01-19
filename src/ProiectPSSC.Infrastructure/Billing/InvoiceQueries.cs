using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Billing;
using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Orders;
using ProiectPSSC.Infrastructure.Persistence;

namespace ProiectPSSC.Infrastructure.Billing;

public sealed class InvoiceQueries : IInvoiceQueries
{
    private readonly AppDbContext _db;

    public InvoiceQueries(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<InvoiceDto>> GetByIdAsync(Guid invoiceId, CancellationToken ct)
    {
        var dto = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new InvoiceDto(
                i.Id,
                i.Number,
                i.OrderId.Value,
                i.BillingEmail,
                i.Currency.ToString(),
                i.Lines.Sum(l => l.Quantity * l.UnitPrice),
                i.Status.ToString(),
                i.CreatedAt,
                i.DueDate,
                i.Lines.Select(l => new InvoiceLineDto(l.ProductCode, l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice)).ToList()
            ))
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result<InvoiceDto>.Fail(Error.NotFound($"Invoice '{invoiceId}' not found"))
            : Result<InvoiceDto>.Ok(dto);
    }

    public async Task<Result<InvoiceDto>> GetByOrderIdAsync(Guid orderId, CancellationToken ct)
    {
        var oid = new OrderId(orderId);

        var dto = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.OrderId == oid)
            .Select(i => new InvoiceDto(
                i.Id,
                i.Number,
                i.OrderId.Value,
                i.BillingEmail,
                i.Currency.ToString(),
                i.Lines.Sum(l => l.Quantity * l.UnitPrice),
                i.Status.ToString(),
                i.CreatedAt,
                i.DueDate,
                i.Lines.Select(l => new InvoiceLineDto(l.ProductCode, l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice)).ToList()
            ))
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result<InvoiceDto>.Fail(Error.NotFound($"Invoice for order '{orderId}' not found"))
            : Result<InvoiceDto>.Ok(dto);
    }
}
