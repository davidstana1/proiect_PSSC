using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ProiectPSSC.Application.Messaging;

namespace ProiectPSSC.Infrastructure.Messaging;

public sealed class InMemoryEventDispatcher : IEventDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;

    public InMemoryEventDispatcher(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task DispatchAsync(string eventType, string payloadJson, CancellationToken ct)
    {
        // Create a scope per event so we can resolve scoped handlers/DbContexts safely.
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        // Simple mapping by event type name.
        // For a course project this is fine; later we can add a registry.
        switch (eventType)
        {
            case "OrderPlaced":
                {
                    var ev = JsonSerializer.Deserialize<Domain.Events.OrderPlaced>(payloadJson)!;
                    var handler = sp.GetRequiredService<IEventHandler<Domain.Events.OrderPlaced>>();
                    await handler.HandleAsync(ev, ct);
                    break;
                }
            case "InvoiceCreated":
                {
                    var ev = JsonSerializer.Deserialize<Domain.Events.InvoiceCreated>(payloadJson)!;
                    var handler = sp.GetRequiredService<IEventHandler<Domain.Events.InvoiceCreated>>();
                    await handler.HandleAsync(ev, ct);
                    break;
                }
            case "OrderCancelled":
                {
                    var ev = JsonSerializer.Deserialize<Domain.Events.OrderCancelled>(payloadJson)!;
                    // Optional handler: cancellation may not trigger anything yet.
                    var handler = sp.GetService<IEventHandler<Domain.Events.OrderCancelled>>();
                    if (handler is not null)
                        await handler.HandleAsync(ev, ct);
                    break;
                }
            default:
                throw new InvalidOperationException($"Unknown event type '{eventType}'");
        }
    }
}
