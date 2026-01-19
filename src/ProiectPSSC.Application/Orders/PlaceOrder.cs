using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Application.Orders;

public sealed record PlaceOrderRequest(string CustomerEmail, List<PlaceOrderLine> Lines);

public sealed record PlaceOrderLine(string ProductCode, int Quantity, decimal UnitPrice)
{
    public OrderLine ToDomain() => new(ProductCode, Quantity, UnitPrice);
}

public sealed record PlaceOrderResponse(Guid OrderId, decimal Total);

public interface IOrderService
{
    Task<Result<PlaceOrderResponse>> PlaceAsync(PlaceOrderRequest request, CancellationToken ct);
}
