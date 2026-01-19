using ProiectPSSC.Domain.Common;

namespace ProiectPSSC.Application.Orders;

public sealed record CancelOrderResponse(Guid OrderId, string Status);

public interface ICancelOrderService
{
    Task<Result<CancelOrderResponse>> CancelAsync(Guid orderId, string? reason, CancellationToken ct);
}

