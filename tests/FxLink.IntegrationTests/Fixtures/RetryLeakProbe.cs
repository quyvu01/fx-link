using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;

namespace FxLink.IntegrationTests.Fixtures;

// Used by RabbitMqRetryHeaderLeakTests only. Static state is keyed by Id so concurrent tests can't
// see each other's messages.
public sealed class RetryLeakTrigger
{
    public Guid Id { get; init; }
}

public sealed class RetryLeakFollowUp
{
    public Guid Id { get; init; }
}

// Fails its first delivery (so RetryPipelineBehavior republishes it with retry headers), then on the
// retried delivery succeeds and publishes a follow-up message from inside the consumer.
public sealed class RetryLeakTriggerConsumer : IConsumer<RetryLeakTrigger>
{
    public static readonly ConcurrentDictionary<Guid, int> Attempts = new();

    public async Task ConsumeAsync(IConsumeContext<RetryLeakTrigger> context, CancellationToken token = default)
    {
        var attempt = Attempts.AddOrUpdate(context.Message.Id, 1, (_, count) => count + 1);
        if (attempt == 1) throw new InvalidOperationException("Simulated first-delivery failure");
        await context.PublishAsync(new RetryLeakFollowUp { Id = context.Message.Id }, token);
    }
}

public sealed class RetryLeakFollowUpConsumer : IConsumer<RetryLeakFollowUp>
{
    public static readonly ConcurrentDictionary<Guid, (string DeliveryKind, int RetryCount)> Received = new();

    public Task ConsumeAsync(IConsumeContext<RetryLeakFollowUp> context, CancellationToken token = default)
    {
        Received[context.Message.Id] = (
            context.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey),
            context.Headers.Get<int>(DistributedConfigurators.Headers.RetryCountKey));
        return Task.CompletedTask;
    }
}
