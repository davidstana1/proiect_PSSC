using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Domain.Billing;

public sealed class Invoice
{
    private readonly List<InvoiceLine> _lines = new();

    public Guid Id { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public OrderId OrderId { get; private set; }
    public string BillingEmail { get; private set; } = string.Empty;
    public Currency Currency { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public decimal Amount => _lines.Sum(l => l.LineTotal);

    private Invoice()
    {
        // EF Core
        Id = default;
        OrderId = default;
        CreatedAt = DateTimeOffset.UtcNow;
        DueDate = DateTimeOffset.UtcNow;
        Currency = Currency.RON;
        Status = InvoiceStatus.Created;
    }

    private Invoice(Guid id, string number, OrderId orderId, string billingEmail, Currency currency, DateTimeOffset dueDate, IEnumerable<InvoiceLine> lines)
    {
        Id = id;
        Number = number;
        OrderId = orderId;
        BillingEmail = billingEmail;
        Currency = currency;
        CreatedAt = DateTimeOffset.UtcNow;
        DueDate = dueDate;
        Status = InvoiceStatus.Created;
        _lines.AddRange(lines);
    }

    public static Invoice Create(Guid invoiceId, string number, OrderId orderId, string billingEmail, Currency currency, DateTimeOffset dueDate, IEnumerable<InvoiceLine> lines)
        => new(invoiceId, number, orderId, billingEmail, currency, dueDate, lines);

    /// <summary>
    /// Updates the invoice lines to reflect order changes.
    /// Only allowed when invoice is in Created status.
    /// </summary>
    public Result UpdateLines(IEnumerable<InvoiceLine> newLines)
    {
        if (Status != InvoiceStatus.Created)
            return Result.Fail(Error.Conflict($"Cannot update invoice in status {Status}"));

        _lines.Clear();
        _lines.AddRange(newLines);
        return Result.Ok();
    }

    /// <summary>
    /// Cancels the invoice.
    /// </summary>
    public Result Cancel()
    {
        if (Status == InvoiceStatus.Cancelled)
            return Result.Fail(Error.Conflict("Invoice already cancelled"));

        Status = InvoiceStatus.Cancelled;
        return Result.Ok();
    }
}
