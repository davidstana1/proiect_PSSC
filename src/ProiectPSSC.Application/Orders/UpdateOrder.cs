using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Application.Orders;

public sealed record UpdateOrderRequest(List<UpdateOrderLine> Lines);

public sealed record UpdateOrderLine(string ProductCode, int Quantity, decimal UnitPrice)
{
    public OrderLine ToDomain() => new(ProductCode, Quantity, UnitPrice);
}

public sealed record UpdateOrderResponse(Guid OrderId, decimal OldTotal, decimal NewTotal);

public interface IUpdateOrderService
{
    Task<Result<UpdateOrderResponse>> UpdateAsync(Guid orderId, UpdateOrderRequest request, CancellationToken ct);
}
