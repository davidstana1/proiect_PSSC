using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Billing;
using ProiectPSSC.Application.Messaging;
using ProiectPSSC.Application.Orders;
using ProiectPSSC.Application.Shipping;
using ProiectPSSC.Infrastructure.Billing;
using ProiectPSSC.Infrastructure.Messaging;
using ProiectPSSC.Infrastructure.Outbox;
using ProiectPSSC.Infrastructure.Orders;
using ProiectPSSC.Infrastructure.Persistence;
using ProiectPSSC.Infrastructure.Shipping;
using ProiectPSSC.Infrastructure.Workflow.Billing;
using ProiectPSSC.Infrastructure.Workflow.Shipping;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Host=localhost;Database=psscdb;Username=pssc;Password=pssc";

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));

// Application services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICancelOrderService, CancelOrderService>();
builder.Services.AddScoped<IUpdateOrderService, UpdateOrderService>();
builder.Services.AddScoped<IShipOrderService, ShipOrderService>();
builder.Services.AddScoped<IInvoiceQueries, InvoiceQueries>();
builder.Services.AddScoped<IShipmentQueries, ShipmentQueries>();
builder.Services.AddSingleton<IEventDispatcher, InMemoryEventDispatcher>();

// Event handlers (workflows)
builder.Services.AddScoped<IEventHandler<ProiectPSSC.Domain.Events.OrderPlaced>, OnOrderPlacedCreateInvoice>();
builder.Services.AddScoped<IEventHandler<ProiectPSSC.Domain.Events.InvoiceCreated>, OnInvoiceCreatedScheduleShipment>();
builder.Services.AddScoped<IEventHandler<ProiectPSSC.Domain.Events.OrderUpdated>, OnOrderUpdatedUpdateInvoice>();

// Outbox background worker
builder.Services.AddHostedService<OutboxDispatcher>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// In demo we keep HTTPS redirection off inside docker; locally it's fine.
app.UseHttpsRedirection();

// Minimal ordering API
app.MapPost("/orders", async (PlaceOrderRequest req, IOrderService svc, CancellationToken ct) =>
{
    var result = await svc.PlaceAsync(req, ct);
    return result.IsSuccess
        ? Results.Created($"/orders/{result.Value!.OrderId}", result.Value)
        : Results.BadRequest(new { error = result.Error });
})
.WithName("PlaceOrder")
.WithOpenApi();

app.MapGet("/orders", async (AppDbContext db, CancellationToken ct) =>
{
    var orders = await db.Orders
        .AsNoTracking()
        .OrderByDescending(o => o.CreatedAt)
        .Select(o => new
        {
            orderId = o.Id.Value,
            customerEmail = o.CustomerEmail,
            status = o.Status.ToString(),
            createdAt = o.CreatedAt,
            total = o.Lines.Sum(l => l.Quantity * l.UnitPrice)
        })
        .ToListAsync(ct);

    return Results.Ok(orders);
})
.WithName("GetOrders")
.WithOpenApi();

app.MapGet("/orders/{orderId:guid}", async (Guid orderId, AppDbContext db, CancellationToken ct) =>
{
    var id = new ProiectPSSC.Domain.Orders.OrderId(orderId);
    var order = await db.Orders
        .AsNoTracking()
        .Where(o => o.Id == id)
        .Select(o => new
        {
            orderId = o.Id.Value,
            customerEmail = o.CustomerEmail,
            status = o.Status.ToString(),
            createdAt = o.CreatedAt,
            lines = o.Lines.Select(l => new
            {
                productCode = l.ProductCode,
                quantity = l.Quantity,
                unitPrice = l.UnitPrice,
                lineTotal = l.Quantity * l.UnitPrice
            }).ToList(),
            total = o.Lines.Sum(l => l.Quantity * l.UnitPrice)
        })
        .FirstOrDefaultAsync(ct);

    return order is null ? Results.NotFound() : Results.Ok(order);
})
.WithName("GetOrderById")
.WithOpenApi();

app.MapPost("/orders/{orderId:guid}/cancel", async (Guid orderId, CancelOrderRequest req, ICancelOrderService svc, CancellationToken ct) =>
{
    var result = await svc.CancelAsync(orderId, req.Reason, ct);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : result.Error?.Code == "not_found"
            ? Results.NotFound(new { error = result.Error })
            : Results.BadRequest(new { error = result.Error });
})
.WithName("CancelOrder")
.WithOpenApi();

app.MapGet("/invoices/{invoiceId:guid}", async (Guid invoiceId, IInvoiceQueries queries, CancellationToken ct) =>
{
    var result = await queries.GetByIdAsync(invoiceId, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { error = result.Error });
})
.WithName("GetInvoiceById")
.WithOpenApi();

app.MapGet("/orders/{orderId:guid}/invoice", async (Guid orderId, IInvoiceQueries queries, CancellationToken ct) =>
{
    var result = await queries.GetByOrderIdAsync(orderId, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { error = result.Error });
})
.WithName("GetInvoiceByOrderId")
.WithOpenApi();

// PUT /orders/{id} - Update order
app.MapPut("/orders/{orderId:guid}", async (Guid orderId, UpdateOrderRequest req, IUpdateOrderService svc, CancellationToken ct) =>
{
    var result = await svc.UpdateAsync(orderId, req, ct);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : result.Error?.Code == "not_found"
            ? Results.NotFound(new { error = result.Error })
            : Results.BadRequest(new { error = result.Error });
})
.WithName("UpdateOrder")
.WithOpenApi();

// POST /orders/{id}/ship - Ship/release order
app.MapPost("/orders/{orderId:guid}/ship", async (Guid orderId, ShipOrderRequest req, IShipOrderService svc, CancellationToken ct) =>
{
    var result = await svc.ShipAsync(orderId, req, ct);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : result.Error?.Code == "not_found"
            ? Results.NotFound(new { error = result.Error })
            : Results.BadRequest(new { error = result.Error });
})
.WithName("ShipOrder")
.WithOpenApi();

// GET /orders/{id}/shipment - Get shipment details
app.MapGet("/orders/{orderId:guid}/shipment", async (Guid orderId, IShipmentQueries queries, CancellationToken ct) =>
{
    var result = await queries.GetByOrderIdAsync(orderId, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { error = result.Error });
})
.WithName("GetShipmentByOrderId")
.WithOpenApi();

// GET /shipments/{id} - Get shipment by id
app.MapGet("/shipments/{shipmentId:guid}", async (Guid shipmentId, IShipmentQueries queries, CancellationToken ct) =>
{
    var result = await queries.GetByIdAsync(shipmentId, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { error = result.Error });
})
.WithName("GetShipmentById")
.WithOpenApi();

// Apply migrations automatically (dev-friendly). For production you'd do it separately.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

public sealed record CancelOrderRequest(string? Reason);

// For WebApplicationFactory in integration tests
public partial class Program { }
