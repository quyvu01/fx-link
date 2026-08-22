using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Exceptions;
using FxLink.Extensions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.Registries;

public class BatchAccumulatorRegistrationTests
{
    private interface IAccumTestMessage
    {
        string Value { get; }
    }

    private sealed record AccumTestMessage(string Value) : IAccumTestMessage;

    private sealed class Recorder
    {
        public List<string> Received { get; } = [];
        public TaskCompletionSource Signal { get; } = new();
    }

    private sealed class AccumTestBatchConsumer(Recorder recorder) : IConsumer<IBatch<IAccumTestMessage>>
    {
        public Task ConsumeAsync(IConsumeContext<IBatch<IAccumTestMessage>> context, CancellationToken token = default)
        {
            recorder.Received.AddRange(context.Message.Select(x => x.Message.Value));
            recorder.Signal.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class AccumTestBatchConsumerDefinition : ConsumerDefinition<AccumTestBatchConsumer>
    {
        public override void Configure(IConsumerConfigurator<AccumTestBatchConsumer> options)
        {
            options.UseBatching<IAccumTestMessage>(c => c
                .SetMessageLimit(2)
                .SetTimeLimit(TimeSpan.FromMinutes(5))
                .SetConcurrencyLimit(1));
        }
    }

    private static IConsumeContext<IAccumTestMessage> ContextFor(string value) =>
        new ConsumeContext<IAccumTestMessage>(new AccumTestMessage(value), PublishContext.New(), requesterId: null);

    [Fact]
    public async Task Accumulator_is_a_singleton_and_flushing_it_invokes_the_registered_batch_consumer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddFxLink(cfg =>
        {
            cfg.UseInMemory();
            cfg.AddConsumer<AccumTestBatchConsumer>();
            cfg.AddConsumerDefinition<AccumTestBatchConsumerDefinition>();
        });

        await using var provider = services.BuildServiceProvider();

        var accumulator1 = provider
            .GetRequiredKeyedService<IBatchAccumulator<IAccumTestMessage>>(typeof(AccumTestBatchConsumer));
        var accumulator2 = provider
            .GetRequiredKeyedService<IBatchAccumulator<IAccumTestMessage>>(typeof(AccumTestBatchConsumer));
        accumulator1.ShouldBeSameAs(accumulator2);

        await accumulator1.AddAsync(ContextFor("a"));
        await accumulator1.AddAsync(ContextFor("b")); // reaches MessageLimit(2) -> flush

        var recorder = provider.GetRequiredService<Recorder>();
        await recorder.Signal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        recorder.Received.ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Resolving_the_accumulator_throws_a_clear_error_when_UseBatching_was_never_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddFxLink(cfg =>
        {
            cfg.UseInMemory();
            cfg.AddConsumer<AccumTestBatchConsumer>();
            // Deliberately no AddConsumerDefinition<AccumTestBatchConsumerDefinition>() here, so
            // UseBatching<IAccumTestMessage> is never called.
        });

        using var provider = services.BuildServiceProvider();

        Should.Throw<FxLinkException.BatchConsumerMissingBatchOptions>(() =>
            provider.GetRequiredKeyedService<IBatchAccumulator<IAccumTestMessage>>(typeof(AccumTestBatchConsumer)));
    }
}
