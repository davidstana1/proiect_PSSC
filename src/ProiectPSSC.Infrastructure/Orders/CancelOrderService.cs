using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Orders;
using ProiectPSSC.Domain.Common;
using ProiectPSSC.Domain.Events;
using ProiectPSSC.Domain.Orders;
using ProiectPSSC.Infrastructure.Persistence;
using ProiectPSSC.Infrastructure.Persistence.Entities;

namespace ProiectPSSC.Infrastructure.Orders;

public sealed class CancelOrderService : ICancelOrderService
{
    private readonly AppDbContext _db;

    public CancelOrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CancelOrderResponse>> CancelAsync(Guid orderId, string? reason, CancellationToken ct)
    {
        var id = new OrderId(orderId);
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return Result<CancelOrderResponse>.Fail(Error.NotFound($"Order '{orderId}' not found"));

        var cancelResult = order.Cancel();
        if (!cancelResult.IsSuccess)
            return Result<CancelOrderResponse>.Fail(cancelResult.Error!.Value);

        var ev = new OrderCancelled(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            OrderId: order.Id,
            Reason: string.IsNullOrWhiteSpace(reason) ? "cancelled_by_customer" : reason.Trim()
        );

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.OutboxEvents.Add(new OutboxEventEntity
        {
            Id = ev.EventId,
            OccurredAt = ev.OccurredAt,
            Type = nameof(OrderCancelled),
            Payload = JsonSerializer.Serialize(ev),
            Attempts = 0
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<CancelOrderResponse>.Ok(new CancelOrderResponse(order.Id.Value, order.Status.ToString()));
    }
}

