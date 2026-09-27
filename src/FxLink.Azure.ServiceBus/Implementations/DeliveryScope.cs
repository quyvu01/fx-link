using Azure.Messaging.ServiceBus;

namespace FxLink.Azure.ServiceBus.Implementations;

// The delivery currently being handled, flowing with the async call chain through the consumer pipeline. Retry
// needs its subscription (a retry must reach only that one) and dead-letter needs its lock to settle it.
internal sealed class DeliveryScope(string subscription, ProcessMessageEventArgs args)
{
    private static readonly AsyncLocal<DeliveryScope> CurrentScope = new();

    public static DeliveryScope Current
    {
        get => CurrentScope.Value;
        set => CurrentScope.Value = value;
    }

    public string Subscription { get; } = subscription;
    public bool DeadLettered { get; private set; }

    public async Task DeadLetterAsync(string reason, string description)
    {
        // CancellationToken.None: shutting down mustn't leave a poisoned message to be redelivered.
        await args.DeadLetterMessageAsync(args.Message, reason, description, CancellationToken.None);
        DeadLettered = true;
    }
}
