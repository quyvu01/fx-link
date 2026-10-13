using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// Lightweight load test, not a benchmarking framework: publish a few thousand messages
// concurrently and assert every one of them is consumed, with no duplicates, within a time bound.
// No FxMap-doc precedent for this one — it's the user's own ask ("integration AND load tests"),
// kept proportionate to this project's scope rather than building out a full perf-testing rig.
public class RabbitMqLoadTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(LoadTestCollector).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(LoadTestCollector).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(LoadTestCollector).Assembly);
            opts.AddRabbitMq(c => c.Host("localhost", "/"));
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Several_thousand_concurrent_publishes_are_all_consumed_exactly_once_within_a_time_bound()
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 5672))
            return; // No local RabbitMQ container reachable — skipped, not failed.

        await using var provider = BuildProvider();
        var connector = provider.GetRequiredService<IMessageBrokerConnector>();
        var publisher = provider.GetRequiredService<IPublisher>();

        using var cts = new CancellationTokenSource();
        var startTask = connector.StartAsync(cts.Token);
        try
        {
            const int messageCount = 3000;

            await WarmUpAsync(publisher);

            var started = DateTime.UtcNow;
            await Parallel.ForEachAsync(Enumerable.Range(0, messageCount),
                new ParallelOptions { MaxDegreeOfParallelism = 32 },
                async (i, token) => await publisher.PublishAsync(new LoadTestMessage { Sequence = i }, token));
            var publishElapsed = DateTime.UtcNow - started;

            await Polling.WaitUntilAsync(() => LoadTestCollector.Received.Count(s => s >= 0) >= messageCount,
                TimeSpan.FromSeconds(60));
            var totalElapsed = DateTime.UtcNow - started;

            // -1 is WarmUpAsync's own throwaway message — excluded from the counts below.
            LoadTestCollector.Received.Count(s => s >= 0).ShouldBe(messageCount);
            LoadTestCollector.Received.Where(s => s >= 0).Distinct().Count().ShouldBe(messageCount); // no duplicates

            // Not a strict perf assertion (this runs on whatever machine happens to run it) — just
            // a sanity bound that catches a gross regression (e.g. accidental serialization of the
            // publish or consume path) rather than measuring throughput precisely.
            totalElapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60));
            _ = publishElapsed; // kept for local diagnosis if this ever needs tightening
        }
        finally
        {
            // Swallows everything, not just OperationCanceledException: StartAsync's own setup can
            // throw a RabbitMQ.Client-specific exception (AlreadyClosedException,
            // OperationInterruptedException, ...) if StopAsync's connection close lands mid-setup,
            // and a throw from a finally block replaces whatever the try block actually failed
            // with — a bug in this cleanup must never end up masking a real test failure above.
            await cts.CancelAsync();
            try
            {
                await connector.StopAsync();
            }
            catch
            {
            }

            try
            {
                await startTask;
            }
            catch
            {
            }
        }
    }

    private static async Task WarmUpAsync(IPublisher publisher)
    {
        // A publish call returning without throwing does NOT prove the exchange exists yet:
        // BasicPublishAsync doesn't synchronously validate the exchange, so a publish sent before
        // StartAsync's consumer-side DeclareTopologyAsync has run can appear to succeed and only
        // surface as an AlreadyClosedException later, on whichever call next reuses that
        // now-broker-closed channel (which is exactly what the 3000-message burst below would hit).
        // The only real proof of readiness is a full round-trip: publish, and see it consumed.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await publisher.PublishAsync(new LoadTestMessage { Sequence = -1 });
                if (await WaitBrieflyForWarmupDeliveryAsync())
                    return;
            }
            catch
            {
                // Fall through to retry below.
            }

            await Task.Delay(300);
        }

        throw new TimeoutException("Warm-up publish was never consumed — topology never became ready.");
    }

    private static async Task<bool> WaitBrieflyForWarmupDeliveryAsync()
    {
        try
        {
            await Polling.WaitUntilAsync(() => LoadTestCollector.Received.Contains(-1), TimeSpan.FromSeconds(2));
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
