using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ProiectPSSC.Application.Messaging;

namespace ProiectPSSC.Infrastructure.Messaging;

public sealed class InMemoryEventDispatcher : IEventDispatcher
{
    private readonly IServiceProvider _sp;

    public InMemoryEventDispatcher(IServiceProvider sp)
    {
        _sp = sp;
    }

    public async Task DispatchAsync(string eventType, string payloadJson, CancellationToken ct)
    {
        // Simple mapping by event type name.
        // For a course project this is fine; later we can add a registry.
        switch (eventType)
        {
            case "OrderPlaced":
                {
                    var ev = JsonSerializer.Deserialize<Domain.Events.OrderPlaced>(payloadJson)!;
                    var handler = _sp.GetRequiredService<IEventHandler<Domain.Events.OrderPlaced>>();
                    await handler.HandleAsync(ev, ct);
                    break;
                }
            case "InvoiceCreated":
                {
                    var ev = JsonSerializer.Deserialize<Domain.Events.InvoiceCreated>(payloadJson)!;
                    var handler = _sp.GetRequiredService<IEventHandler<Domain.Events.InvoiceCreated>>();
                    await handler.HandleAsync(ev, ct);
                    break;
                }
            default:
                throw new InvalidOperationException($"Unknown event type '{eventType}'");
        }
    }
}
