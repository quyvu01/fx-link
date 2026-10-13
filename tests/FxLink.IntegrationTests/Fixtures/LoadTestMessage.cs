using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Contexts;

namespace FxLink.IntegrationTests.Fixtures;

// Separate from CollectedMessage/CollectorConsumer on purpose: xunit runs test classes in parallel
// by default, and RabbitMqLoadTests sharing a static collection with SqsLifecycleTests would let
// the two corrupt each other's counts.
public sealed class LoadTestMessage
{
    public int Sequence { get; init; }
}

public sealed class LoadTestCollector : IConsumer<LoadTestMessage>
{
    public static readonly ConcurrentBag<int> Received = [];

    public Task ConsumeAsync(IConsumeContext<LoadTestMessage> context, CancellationToken token = default)
    {
        Received.Add(context.Message.Sequence);
        return Task.CompletedTask;
    }
}
