using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Orders;
using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Events;
using ProiectPSSC.Domain.Orders;
using ProiectPSSC.Infrastructure.Persistence;
using ProiectPSSC.Infrastructure.Persistence.Entities;

namespace ProiectPSSC.Infrastructure.Orders;

public sealed class UpdateOrderService : IUpdateOrderService
{
    private readonly AppDbContext _db;

    public UpdateOrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<UpdateOrderResponse>> UpdateAsync(Guid orderId, UpdateOrderRequest request, CancellationToken ct)
    {
        var id = new OrderId(orderId);
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return Result<UpdateOrderResponse>.Fail(Error.NotFound($"Order '{orderId}' not found"));

        var oldTotal = order.Total;

        var domainLines = (request.Lines ?? new List<UpdateOrderLine>()).Select(l => l.ToDomain()).ToList();
        var updateResult = order.Update(domainLines);
        if (!updateResult.IsSuccess)
            return Result<UpdateOrderResponse>.Fail(updateResult.Error!.Value);

        var newTotal = order.Total;

        var orderUpdated = new OrderUpdated(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            OrderId: order.Id,
            CustomerEmail: order.CustomerEmail,
            OldTotal: oldTotal,
            NewTotal: newTotal
        );

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.OutboxEvents.Add(new OutboxEventEntity
        {
            Id = orderUpdated.EventId,
            OccurredAt = orderUpdated.OccurredAt,
            Type = nameof(OrderUpdated),
            Payload = JsonSerializer.Serialize(orderUpdated),
            Attempts = 0
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<UpdateOrderResponse>.Ok(new UpdateOrderResponse(order.Id.Value, oldTotal, newTotal));
    }
}
