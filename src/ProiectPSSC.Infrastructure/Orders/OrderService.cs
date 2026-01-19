using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Orders;
using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Events;
using ProiectPSSC.Domain.Orders;
using ProiectPSSC.Infrastructure.Persistence;
using ProiectPSSC.Infrastructure.Persistence.Entities;

namespace ProiectPSSC.Infrastructure.Orders;

public sealed class OrderService : IOrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PlaceOrderResponse>> PlaceAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        var domainLines = (request.Lines ?? new List<PlaceOrderLine>()).Select(l => l.ToDomain()).ToList();
        var result = Order.Place(request.CustomerEmail, domainLines);
        if (!result.IsSuccess)
            return Result<PlaceOrderResponse>.Fail(result.Error!.Value);

        var order = result.Value!;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.Orders.Add(order);

        var orderPlaced = new OrderPlaced(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            OrderId: order.Id,
            CustomerEmail: order.CustomerEmail,
            Total: order.Total
        );

        _db.OutboxEvents.Add(new OutboxEventEntity
        {
            Id = orderPlaced.EventId,
            OccurredAt = orderPlaced.OccurredAt,
            Type = nameof(OrderPlaced),
            Payload = JsonSerializer.Serialize(orderPlaced),
            Attempts = 0
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<PlaceOrderResponse>.Ok(new PlaceOrderResponse(order.Id.Value, order.Total));
    }
}
