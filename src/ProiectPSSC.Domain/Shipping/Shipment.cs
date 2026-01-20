using ProiectPSSC.Domain.Orders;

namespace ProiectPSSC.Domain.Shipping;

public sealed class Shipment
{
    public Guid Id { get; private set; }
    public OrderId OrderId { get; private set; }
    public string? TrackingNumber { get; private set; }
    public DateTimeOffset ShippedAt { get; private set; }
    public string? Carrier { get; private set; }

    private Shipment()
    {
        // for EF Core
        Id = default;
        OrderId = default;
        ShippedAt = DateTimeOffset.UtcNow;
    }

    private Shipment(Guid id, OrderId orderId, string? trackingNumber, string? carrier)
    {
        Id = id;
        OrderId = orderId;
        TrackingNumber = trackingNumber;
        Carrier = carrier;
        ShippedAt = DateTimeOffset.UtcNow;
    }

    public static Shipment Create(OrderId orderId, string? trackingNumber = null, string? carrier = null)
        => new(Guid.NewGuid(), orderId, trackingNumber, carrier);
}
