using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Orders;
using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Events;
using ProiectPSSC.Domain.Orders;
using ProiectPSSC.Domain.Shipping;
using ProiectPSSC.Infrastructure.Persistence;
using ProiectPSSC.Infrastructure.Persistence.Entities;

namespace ProiectPSSC.Infrastructure.Orders;

public sealed class ShipOrderService : IShipOrderService
{
    private readonly AppDbContext _db;

    public ShipOrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ShipOrderResponse>> ShipAsync(Guid orderId, ShipOrderRequest request, CancellationToken ct)
    {
        var id = new OrderId(orderId);
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return Result<ShipOrderResponse>.Fail(Error.NotFound($"Order '{orderId}' not found"));

        var shipResult = order.Ship();
        if (!shipResult.IsSuccess)
            return Result<ShipOrderResponse>.Fail(shipResult.Error!.Value);

        var shipment = Shipment.Create(order.Id, request.TrackingNumber, request.Carrier);
        _db.Shipments.Add(shipment);

        var orderShipped = new OrderShipped(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            OrderId: order.Id,
            ShipmentId: shipment.Id,
            TrackingNumber: shipment.TrackingNumber
        );

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.OutboxEvents.Add(new OutboxEventEntity
        {
            Id = orderShipped.EventId,
            OccurredAt = orderShipped.OccurredAt,
            Type = nameof(OrderShipped),
            Payload = JsonSerializer.Serialize(orderShipped),
            Attempts = 0
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<ShipOrderResponse>.Ok(new ShipOrderResponse(
            order.Id.Value,
            shipment.Id,
            order.Status.ToString(),
            shipment.ShippedAt
        ));
    }
}
