using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Application.Messaging;
using ProiectPSSC.Application.Orders;
using ProiectPSSC.Infrastructure.Messaging;
using ProiectPSSC.Infrastructure.Outbox;
using ProiectPSSC.Infrastructure.Orders;
using ProiectPSSC.Infrastructure.Persistence;
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
builder.Services.AddSingleton<IEventDispatcher, InMemoryEventDispatcher>();

// Event handlers (workflows)
builder.Services.AddScoped<IEventHandler<ProiectPSSC.Domain.Events.OrderPlaced>, OnOrderPlacedCreateInvoice>();
builder.Services.AddScoped<IEventHandler<ProiectPSSC.Domain.Events.InvoiceCreated>, OnInvoiceCreatedScheduleShipment>();

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

// Apply migrations automatically (dev-friendly). For production you'd do it separately.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
