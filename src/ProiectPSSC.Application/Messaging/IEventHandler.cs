namespace ProiectPSSC.Application.Messaging;

public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent ev, CancellationToken ct);
}
