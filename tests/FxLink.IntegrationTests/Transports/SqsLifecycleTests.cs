using Amazon;
using FxLink.Abstractions;
using FxLink.Aws.Sqs.Abstractions;
using FxLink.Aws.Sqs.Extensions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// Against an isolated local LocalStack container with SNS+SQS enabled (see
// tests/FxLink.IntegrationTests/README.md — deliberately NOT the shared fxmap_localstack_test
// container, whose SNS is disabled and which belongs to a different project).
//
// Same *_a_server_can_be_stopped_and_started_again shape as the RabbitMq/NATS lifecycle tests, but
// this one is the direct regression test for the SqsClient fix in this change: before the fix,
// StartAsync never cleared _queueUrlsByMessageType across restarts, so the delayed-publish fan-out
// set for one message type grew by one duplicate entry per restart, and the old poll loops from a
// run that exited via exception were never cancelled either. SQS doesn't implement IRequester<> yet
// (see AddSqs's own comment), so this uses plain pub/sub instead of request/response.
public class SqsLifecycleTests
{
    private const string LocalStackServiceUrl = "http://localhost:4567";

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(CollectorConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(CollectorConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(CollectorConsumer).Assembly);
            opts.AddSqs(c =>
            {
                c.Region(RegionEndpoint.USEast1, credential =>
                {
                    credential.ServiceUrl(LocalStackServiceUrl);
                    credential.AccessKeyId("test");
                    credential.SecretAccessKey("test");
                });
            });
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_server_can_be_stopped_and_started_again_without_duplicating_queue_subscriptions()
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 4567))
            return; // No isolated LocalStack container reachable — skipped, not failed.

        await using var provider = BuildProvider();
        var connector = provider.GetRequiredService<IMessageBrokerConnector>();
        var sqsClient = (ISqsClient)connector;
        var publisher = provider.GetRequiredService<IPublisher>();

        // LocalStack's SQS long-poll (WaitTimeSeconds=20, see PollLoopAsync) doesn't reliably wake
        // early when a message arrives mid-wait the way real SQS does, so a message published just
        // before a round's restart can easily end up sitting unconsumed until a LATER round's poll
        // happens to pick it up — that's an artifact of this broker's long-poll emulation, not a
        // FxLink bug, so this test doesn't assert "received within this same round". What it does
        // assert: no duplicate queue subscription from the restart (the bug this test is really
        // for), and that every published message is eventually delivered exactly once, never twice
        // (which an orphaned poller from a prior round would cause).
        for (var round = 1; round <= 3; round++)
        {
            using var cts = new CancellationTokenSource();
            var startTask = connector.StartAsync(cts.Token);
            await Polling.WaitUntilAsync(
                () => sqsClient.GetQueueUrlsForMessageType(typeof(CollectedMessage)).Count > 0,
                TimeSpan.FromSeconds(10));
            // The queue-url dictionary above is populated before SubscribeQueueToTopicAsync sets
            // RawMessageDelivery on the SNS subscription (see SqsClient.StartAsync's ordering) — a
            // publish sent in that small gap would arrive SNS-wrapped, which this consumer silently
            // drops (and still deletes) rather than parses. Give the subscription setup a moment to
            // actually finish before publishing.
            await Task.Delay(500);

            await publisher.PublishAsync(new CollectedMessage { Sequence = round });
            await Task.Delay(1000); // give this round's poller a chance, but don't require it

            await cts.CancelAsync();
            await connector.StopAsync();
            try
            {
                await startTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        // The fixed bug: this list used to gain one duplicate entry per restart instead of being
        // cleared, which would have sent every future delayed publish to the same queue 3 times.
        sqsClient.GetQueueUrlsForMessageType(typeof(CollectedMessage)).Count.ShouldBe(1);

        // One more start so whichever messages weren't picked up mid-round (see the comment above)
        // get a long-lived poller to deliver them — generous (comfortably more than two full
        // WaitTimeSeconds=20 long-poll cycles) because LocalStack's long-poll wake-up granularity
        // is coarse, not because real SQS is this slow.
        using var finalCts = new CancellationTokenSource();
        var finalStart = connector.StartAsync(finalCts.Token);
        await Polling.WaitUntilAsync(
            () => new[] { 1, 2, 3 }.All(seq => CollectorConsumer.Received.Count(s => s == seq) == 1),
            TimeSpan.FromSeconds(90));
        await finalCts.CancelAsync();
        await connector.StopAsync();
        try
        {
            await finalStart;
        }
        catch (OperationCanceledException)
        {
        }

        // Exactly once each — never duplicated by an orphaned poller from an earlier round.
        CollectorConsumer.Received.Count(s => s == 1).ShouldBe(1);
        CollectorConsumer.Received.Count(s => s == 2).ShouldBe(1);
        CollectorConsumer.Received.Count(s => s == 3).ShouldBe(1);
    }
}
