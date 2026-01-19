using ProiectPSSC.Application.Messaging;
using ProiectPSSC.Domain.Events;

namespace ProiectPSSC.Infrastructure.Workflow.Shipping;

public sealed class OnInvoiceCreatedScheduleShipment : IEventHandler<InvoiceCreated>
{
    public Task HandleAsync(InvoiceCreated ev, CancellationToken ct)
    {
        // TODO: persist shipment; for now we just simulate with a completed task.
        return Task.CompletedTask;
    }
}
