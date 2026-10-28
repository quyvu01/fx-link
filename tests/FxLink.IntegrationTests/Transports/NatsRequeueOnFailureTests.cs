using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.Nats.Extensions;
using FxLink.Nats.Registries;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// INatsConfigurator.RequeueOnFailure: an exception escaping the consumer pipeline used to ack the
// message (lost). Same shape as RabbitMqRequeueOnFailureTests; a unique stream per test isolates it
// the way a throwaway vhost does there (see NatsLifecycleTests).
public class NatsRequeueOnFailureTests
{
    private const string NatsUrl = "nats://localhost:4222";

    private static ServiceProvider BuildProvider(string streamName, Action<INatsConfigurator> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddConsumerPipelineBehaviors(c => c.Of<FailFirstDeliveryBehavior>());
            opts.AddNats(c =>
            {
                c.Server(NatsUrl);
                c.StreamName(streamName);
                c.SubjectPrefix(streamName.ToLowerInvariant());
                configure?.Invoke(c);
            });
        });
        return services.BuildServiceProvider();
    }

    private static async Task RunAsync(Action<INatsConfigurator> configure,
        Func<Guid, Func<Task<long>>, Task> assert)
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 4222))
            return; // No local NATS container reachable — skipped, not failed.

        var streamName = $"FXLINK_RQ_{Guid.NewGuid():N}".ToUpperInvariant()[..20];
        await using var nats = new NatsConnection(new NatsOpts { Url = NatsUrl });
        var js = new NatsJSContext(nats);

        await using var provider = BuildProvider(streamName, configure);
        var connector = provider.GetRequiredService<IMessageBrokerConnector>();
        var publisher = provider.GetRequiredService<IPublisher>();

        using var cts = new CancellationTokenSource();
        var startTask = connector.StartAsync(cts.Token);
        try
        {
            // Consumers only receive what's published after they exist (DeliverPolicy.New), so wait for
            // the probe's consumer before publishing.
            string consumerName = null;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
            while (consumerName is null)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Probe consumer was never created.");
                try
                {
                    await foreach (var name in js.ListConsumerNamesAsync(streamName))
                        if (name.Contains(nameof(RequeueProbeConsumer), StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("dlq", StringComparison.OrdinalIgnoreCase))
                            consumerName = name;
                }
                catch (NatsJSApiException)
                {
                    // Stream not created yet.
                }

                if (consumerName is null) await Task.Delay(200);
            }

            var id = Guid.NewGuid();
            await publisher.PublishAsync(new RequeueProbe { Id = id });
            await assert(id, async () =>
            {
                var consumer = await js.GetConsumerAsync(streamName, consumerName);
                return (long)consumer.Info.NumAckPending + (long)consumer.Info.NumPending;
            });
        }
        finally
        {
            await cts.CancelAsync();
            try
            {
                await connector.StopAsync();
                await startTask;
            }
            catch
            {
            }

            try
            {
                await js.DeleteStreamAsync(streamName);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public Task A_message_whose_pipeline_throws_is_redelivered_when_RequeueOnFailure_is_configured() =>
        RunAsync(c => c.RequeueOnFailure(TimeSpan.FromMilliseconds(500)), async (id, _) =>
        {
            await Polling.WaitUntilAsync(() => RequeueProbeConsumer.Received.Contains(id), TimeSpan.FromSeconds(20));
            FailFirstDeliveryBehavior.Attempts[id].ShouldBe(2);
        });

    [Fact]
    public Task A_message_whose_pipeline_throws_is_acked_and_lost_by_default() =>
        RunAsync(null, async (id, outstanding) =>
        {
            await Polling.WaitUntilAsync(() => FailFirstDeliveryBehavior.Attempts.ContainsKey(id),
                TimeSpan.FromSeconds(20));
            // Acked rather than left pending: nothing outstanding on the consumer.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (await outstanding() > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(200);
            (await outstanding()).ShouldBe(0L);
            FailFirstDeliveryBehavior.Attempts[id].ShouldBe(1);
            RequeueProbeConsumer.Received.ShouldNotContain(id);
        });
}
