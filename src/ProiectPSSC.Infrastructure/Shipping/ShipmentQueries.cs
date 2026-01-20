using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Shipping;
using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Orders;
using ProiectPSSC.Infrastructure.Persistence;

namespace ProiectPSSC.Infrastructure.Shipping;

public sealed class ShipmentQueries : IShipmentQueries
{
    private readonly AppDbContext _db;

    public ShipmentQueries(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ShipmentDto>> GetByOrderIdAsync(Guid orderId, CancellationToken ct)
    {
        var id = new OrderId(orderId);
        var shipment = await _db.Shipments
            .AsNoTracking()
            .Where(s => s.OrderId == id)
            .Select(s => new ShipmentDto(
                s.Id,
                s.OrderId.Value,
                s.TrackingNumber,
                s.Carrier,
                s.ShippedAt
            ))
            .FirstOrDefaultAsync(ct);

        if (shipment is null)
            return Result<ShipmentDto>.Fail(Error.NotFound($"Shipment for order '{orderId}' not found"));

        return Result<ShipmentDto>.Ok(shipment);
    }

    public async Task<Result<ShipmentDto>> GetByIdAsync(Guid shipmentId, CancellationToken ct)
    {
        var shipment = await _db.Shipments
            .AsNoTracking()
            .Where(s => s.Id == shipmentId)
            .Select(s => new ShipmentDto(
                s.Id,
                s.OrderId.Value,
                s.TrackingNumber,
                s.Carrier,
                s.ShippedAt
            ))
            .FirstOrDefaultAsync(ct);

        if (shipment is null)
            return Result<ShipmentDto>.Fail(Error.NotFound($"Shipment '{shipmentId}' not found"));

        return Result<ShipmentDto>.Ok(shipment);
    }
}
