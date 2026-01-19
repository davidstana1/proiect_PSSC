using ProiectPSSC.Domain.Common;

namespace ProiectPSSC.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderLine> _lines = new();

    public OrderId Id { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    private Order()
    {
        // for EF Core
        Id = default;
        Status = OrderStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private Order(OrderId id, string customerEmail, IEnumerable<OrderLine> lines)
    {
        Id = id;
        CustomerEmail = customerEmail;
        _lines.AddRange(lines);
        Status = OrderStatus.Placed;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public decimal Total => _lines.Sum(l => l.LineTotal);

    public static Result<Order> Place(string customerEmail, IEnumerable<OrderLine> lines)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
            return Result<Order>.Fail(Error.Validation("Customer email is required"));

        var materialized = lines?.ToList() ?? new List<OrderLine>();
        if (materialized.Count == 0)
            return Result<Order>.Fail(Error.Validation("At least one order line is required"));

        if (materialized.Any(l => l.Quantity <= 0))
            return Result<Order>.Fail(Error.Validation("Quantity must be > 0"));

        if (materialized.Any(l => l.UnitPrice < 0))
            return Result<Order>.Fail(Error.Validation("UnitPrice must be >= 0"));

        var order = new Order(OrderId.New(), customerEmail.Trim(), materialized);
        return Result<Order>.Ok(order);
    }

    public Result Cancel()
    {
        if (Status == OrderStatus.Cancelled)
            return Result.Fail(Error.Conflict("Order already cancelled"));

        Status = OrderStatus.Cancelled;
        return Result.Ok();
    }
}
