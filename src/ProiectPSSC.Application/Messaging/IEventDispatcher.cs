namespace ProiectPSSC.Application.Messaging;

public interface IEventDispatcher
{
    Task DispatchAsync(string eventType, string payloadJson, CancellationToken ct);
}
