using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Contexts;

namespace FxLink.IntegrationTests.Fixtures;

// Plain pub/sub fixture (no request/response) — used for SQS, which doesn't implement IRequester<>
// (see FxLink.Aws.Sqs's DependencyExtensions.AddSqs comment), and for the load tests, where
// publish-and-count is all that's needed. CollectorConsumer.Received is static and process-wide on
// purpose: StartAsync resolves a fresh scoped/singleton graph each round, so a test can't hold a
// per-instance reference to "the" consumer the way it can hold a reference to a FakeServer.
public sealed class CollectedMessage
{
    public int Sequence { get; init; }
}

public sealed class CollectorConsumer : IConsumer<CollectedMessage>
{
    public static readonly ConcurrentBag<int> Received = [];

    public Task ConsumeAsync(IConsumeContext<CollectedMessage> context, CancellationToken token = default)
    {
        Received.Add(context.Message.Sequence);
        return Task.CompletedTask;
    }
}
