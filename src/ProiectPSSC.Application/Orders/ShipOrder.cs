using ProiectPSSC.Domain.Common;

namespace ProiectPSSC.Application.Orders;

public sealed record ShipOrderRequest(string? TrackingNumber, string? Carrier);

public sealed record ShipOrderResponse(Guid OrderId, Guid ShipmentId, string Status, DateTimeOffset ShippedAt);

public interface IShipOrderService
{
    Task<Result<ShipOrderResponse>> ShipAsync(Guid orderId, ShipOrderRequest request, CancellationToken ct);
}
