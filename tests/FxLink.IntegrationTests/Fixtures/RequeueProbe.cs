using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;

namespace FxLink.IntegrationTests.Fixtures;

// Used by the *RequeueOnFailureTests only. Static state is keyed by Id, so concurrent tests
// (each publishing its own Ids) can't see each other's attempts.
public sealed class RequeueProbe
{
    public Guid Id { get; init; }
}

public sealed class RequeueProbeConsumer : IConsumer<RequeueProbe>
{
    public static readonly ConcurrentBag<Guid> Received = [];

    public Task ConsumeAsync(IConsumeContext<RequeueProbe> context, CancellationToken token = default)
    {
        Received.Add(context.Message.Id);
        return Task.CompletedTask;
    }
}

// Throws on the first delivery of each message. Registered from inside the AddFxLink callback, so it
// runs outside RetryPipelineBehavior and its exception escapes the whole consumer pipeline — the
// situation RequeueOnFailure exists for (e.g. the retry republish or the Inbox store failing).
public sealed class FailFirstDeliveryBehavior : IConsumerPipelineBehavior<RequeueProbe>
{
    public static readonly ConcurrentDictionary<Guid, int> Attempts = new();

    public Task ConsumeAsync(IConsumeContext<RequeueProbe> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        var attempt = Attempts.AddOrUpdate(context.Message.Id, 1, (_, count) => count + 1);
        if (attempt == 1) throw new InvalidOperationException("Simulated infrastructure failure");
        return next.Invoke(token);
    }
}
