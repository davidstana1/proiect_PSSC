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

    /// <summary>
    /// Returns true if the order is in an editable state.
    /// </summary>
    public bool IsEditable => Status == OrderStatus.Placed || Status == OrderStatus.Invoiced;

    /// <summary>
    /// Updates the order lines. Only allowed in editable statuses.
    /// </summary>
    public Result Update(IEnumerable<OrderLine> newLines)
    {
        if (Status == OrderStatus.Cancelled)
            return Result.Fail(Error.Conflict("Cannot update a cancelled order"));

        if (Status == OrderStatus.Shipped)
            return Result.Fail(Error.Conflict("Cannot update a shipped order"));

        var materialized = newLines?.ToList() ?? new List<OrderLine>();
        if (materialized.Count == 0)
            return Result.Fail(Error.Validation("At least one order line is required"));

        if (materialized.Any(l => l.Quantity <= 0))
            return Result.Fail(Error.Validation("Quantity must be > 0"));

        if (materialized.Any(l => l.UnitPrice < 0))
            return Result.Fail(Error.Validation("UnitPrice must be >= 0"));

        _lines.Clear();
        _lines.AddRange(materialized);

        return Result.Ok();
    }

    /// <summary>
    /// Marks the order as invoiced.
    /// </summary>
    public Result MarkAsInvoiced()
    {
        if (Status != OrderStatus.Placed)
            return Result.Fail(Error.Conflict($"Cannot invoice order in status {Status}"));

        Status = OrderStatus.Invoiced;
        return Result.Ok();
    }

    /// <summary>
    /// Ships/releases the order. Only invoiced orders can be shipped.
    /// </summary>
    public Result Ship()
    {
        if (Status == OrderStatus.Cancelled)
            return Result.Fail(Error.Conflict("Cannot ship a cancelled order"));

        if (Status == OrderStatus.Shipped)
            return Result.Fail(Error.Conflict("Order already shipped"));

        if (Status != OrderStatus.Invoiced)
            return Result.Fail(Error.Conflict("Order must be invoiced before shipping"));

        Status = OrderStatus.Shipped;
        return Result.Ok();
    }

    public Result Cancel()
    {
        if (Status == OrderStatus.Cancelled)
            return Result.Fail(Error.Conflict("Order already cancelled"));

        if (Status == OrderStatus.Shipped)
            return Result.Fail(Error.Conflict("Cannot cancel a shipped order"));

        Status = OrderStatus.Cancelled;
        return Result.Ok();
    }
}
