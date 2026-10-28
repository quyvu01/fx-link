using Amazon;
using Amazon.SQS;
using FxLink.Abstractions;
using FxLink.Aws.Sqs.Abstractions;
using FxLink.Aws.Sqs.Extensions;
using FxLink.Aws.Sqs.Registries;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// ISqsConfigurator.RequeueOnFailure: an exception escaping the consumer pipeline used to delete the
// message (lost). Same shape as RabbitMqRequeueOnFailureTests, against the isolated LocalStack
// container (see SqsLifecycleTests). Test classes don't run in parallel (AssemblyInfo.cs), so no other
// provider is polling the shared RequeueProbeConsumer queue meanwhile.
public class SqsRequeueOnFailureTests
{
    private const string LocalStackServiceUrl = "http://localhost:4567";

    private static ServiceProvider BuildProvider(Action<ISqsConfigurator> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddConsumerPipelineBehaviors(c => c.Of<FailFirstDeliveryBehavior>());
            opts.AddSqs(c =>
            {
                c.Region(RegionEndpoint.USEast1, credential =>
                {
                    credential.ServiceUrl(LocalStackServiceUrl);
                    credential.AccessKeyId("test");
                    credential.SecretAccessKey("test");
                });
                configure?.Invoke(c);
            });
        });
        return services.BuildServiceProvider();
    }

    private static async Task RunAsync(Action<ISqsConfigurator> configure, Func<Guid, string, Task> assert)
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 4567))
            return; // No isolated LocalStack container reachable — skipped, not failed.

        await using var provider = BuildProvider(configure);
        var connector = provider.GetRequiredService<IMessageBrokerConnector>();
        var sqsClient = (ISqsClient)connector;
        var publisher = provider.GetRequiredService<IPublisher>();

        using var cts = new CancellationTokenSource();
        var startTask = connector.StartAsync(cts.Token);
        try
        {
            await Polling.WaitUntilAsync(
                () => sqsClient.GetQueueUrlsForMessageType(typeof(RequeueProbe)).Count > 0,
                TimeSpan.FromSeconds(10));
            // Let the SNS subscription's RawMessageDelivery settle first (see SqsLifecycleTests).
            await Task.Delay(500);

            var id = Guid.NewGuid();
            await publisher.PublishAsync(new RequeueProbe { Id = id });
            await assert(id, sqsClient.GetQueueUrlsForMessageType(typeof(RequeueProbe))[0]);
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
        }
    }

    private static async Task<int> CountMessagesAsync(string queueUrl)
    {
        using var sqs = new AmazonSQSClient("test", "test",
            new AmazonSQSConfig { ServiceURL = LocalStackServiceUrl, AuthenticationRegion = "us-east-1" });
        var response = await sqs.GetQueueAttributesAsync(queueUrl,
            ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"]);
        return int.Parse(response.Attributes["ApproximateNumberOfMessages"]) +
               int.Parse(response.Attributes["ApproximateNumberOfMessagesNotVisible"]);
    }

    [Fact]
    public Task A_message_whose_pipeline_throws_is_redelivered_when_RequeueOnFailure_is_configured() =>
        RunAsync(c => c.RequeueOnFailure(TimeSpan.FromSeconds(1)), async (id, _) =>
        {
            // Generous: LocalStack's long-poll doesn't reliably wake early (see SqsLifecycleTests).
            await Polling.WaitUntilAsync(() => RequeueProbeConsumer.Received.Contains(id), TimeSpan.FromSeconds(60),
                pollMs: 200);
            FailFirstDeliveryBehavior.Attempts[id].ShouldBe(2);
        });

    [Fact]
    public Task A_message_whose_pipeline_throws_is_deleted_and_lost_by_default() =>
        RunAsync(null, async (id, queueUrl) =>
        {
            await Polling.WaitUntilAsync(() => FailFirstDeliveryBehavior.Attempts.ContainsKey(id),
                TimeSpan.FromSeconds(60), pollMs: 200);
            // Deleted rather than left in flight: nothing visible or invisible remains on the queue.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (await CountMessagesAsync(queueUrl) > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(200);
            (await CountMessagesAsync(queueUrl)).ShouldBe(0);
            FailFirstDeliveryBehavior.Attempts[id].ShouldBe(1);
            RequeueProbeConsumer.Received.ShouldNotContain(id);
        });
}
