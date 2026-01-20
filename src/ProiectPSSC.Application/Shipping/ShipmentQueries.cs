using ProiectPSSC.Domain.Common;

namespace ProiectPSSC.Application.Shipping;

public sealed record ShipmentDto(
    Guid ShipmentId,
    Guid OrderId,
    string? TrackingNumber,
    string? Carrier,
    DateTimeOffset ShippedAt
);

public interface IShipmentQueries
{
    Task<Result<ShipmentDto>> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
    Task<Result<ShipmentDto>> GetByIdAsync(Guid shipmentId, CancellationToken ct);
}
