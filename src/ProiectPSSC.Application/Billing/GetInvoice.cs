using ProiectPSSC.Domain.Common;

namespace ProiectPSSC.Application.Billing;

public sealed record InvoiceLineDto(string ProductCode, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record InvoiceDto(
    Guid InvoiceId,
    string Number,
    Guid OrderId,
    string BillingEmail,
    string Currency,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset DueDate,
    List<InvoiceLineDto> Lines
);

public interface IInvoiceQueries
{
    Task<Result<InvoiceDto>> GetByIdAsync(Guid invoiceId, CancellationToken ct);
    Task<Result<InvoiceDto>> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
}
