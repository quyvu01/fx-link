using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Extensions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Tests.PipelineBehaviors;

public class ConsumerPipelineBehaviorOrchestratorBatchTests
{
    private interface IOrchestratedBatchMessage
    {
        string Value { get; }
    }

    private sealed record OrchestratedBatchMessage(string Value) : IOrchestratedBatchMessage;

    private sealed class Recorder
    {
        public List<string> Received { get; } = [];
        public TaskCompletionSource Signal { get; } = new();
    }

    private sealed class OrchestratedBatchConsumer(Recorder recorder) : IConsumer<IBatch<IOrchestratedBatchMessage>>
    {
        public Task ConsumeAsync(IConsumeContext<IBatch<IOrchestratedBatchMessage>> context,
            CancellationToken token = default)
        {
            recorder.Received.AddRange(context.Message.Select(x => x.Message.Value));
            recorder.Signal.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class OrchestratedBatchConsumerDefinition : ConsumerDefinition<OrchestratedBatchConsumer>
    {
        public override void Configure(IConsumerConfigurator<OrchestratedBatchConsumer> options)
        {
            options.UseBatching<IOrchestratedBatchMessage>(c => c
                .SetMessageLimit(2)
                .SetTimeLimit(TimeSpan.FromMinutes(5))
                .SetConcurrencyLimit(1));
        }
    }

    private static IConsumeContext<IOrchestratedBatchMessage> ContextFor(string value) =>
        new ConsumeContext<IOrchestratedBatchMessage>(new OrchestratedBatchMessage(value), PublishContext.New(),
            requesterId: null);

    [Fact]
    public async Task Individual_messages_sent_through_the_normal_ConsumerConnector_path_accumulate_and_flush_as_a_batch()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddFxLink(cfg =>
        {
            cfg.UseInMemory();
            cfg.AddConsumer<OrchestratedBatchConsumer>();
            cfg.AddConsumerDefinition<OrchestratedBatchConsumerDefinition>();
        });

        await using var provider = services.BuildServiceProvider();
        var connector = provider.GetRequiredService<IConsumerConnector<IOrchestratedBatchMessage>>();

        // This is exactly what a transport (RabbitMqClientConnector.ProcessMessageReceivedAsync,
        // InMemoryClientConnector) does per individual wire message — no batch awareness at all.
        // The orchestrator branch is what has to notice and divert these.
        await connector.ConsumeAsync(ContextFor("a"), typeof(OrchestratedBatchConsumer));
        await connector.ConsumeAsync(ContextFor("b"), typeof(OrchestratedBatchConsumer));

        var recorder = provider.GetRequiredService<Recorder>();
        await recorder.Signal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        recorder.Received.ShouldBe(["a", "b"]);
    }

    private interface IPlainMessage
    {
        string Value { get; }
    }

    private sealed record PlainMessage(string Value) : IPlainMessage;

    private sealed class PlainConsumer(Recorder recorder) : IConsumer<IPlainMessage>
    {
        public Task ConsumeAsync(IConsumeContext<IPlainMessage> context, CancellationToken token = default)
        {
            recorder.Received.Add(context.Message.Value);
            recorder.Signal.TrySetResult();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_non_batch_consumer_is_unaffected_by_the_new_accumulator_lookup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddFxLink(cfg =>
        {
            cfg.UseInMemory();
            cfg.AddConsumer<PlainConsumer>();
        });

        await using var provider = services.BuildServiceProvider();
        var connector = provider.GetRequiredService<IConsumerConnector<IPlainMessage>>();

        await connector.ConsumeAsync(
            new ConsumeContext<IPlainMessage>(new PlainMessage("solo"), PublishContext.New(), requesterId: null),
            typeof(PlainConsumer));

        var recorder = provider.GetRequiredService<Recorder>();
        await recorder.Signal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        recorder.Received.ShouldBe(["solo"]);
    }
}
