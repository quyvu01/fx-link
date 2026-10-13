using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.Nats.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// Against the real local NATS (JetStream) container (see tests/FxLink.IntegrationTests/README.md).
// Same shape as RabbitMqLifecycleTests — see its header comment. A unique subject prefix/stream
// per run avoids colliding with any other consumer definitions a previous run of this same test
// left on the shared broker (JetStream streams aren't torn down by this test).
public class NatsLifecycleTests
{
    private static ServiceProvider BuildProvider(string streamName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddNats(c =>
            {
                c.Server("nats://localhost:4222");
                c.StreamName(streamName);
                c.SubjectPrefix(streamName.ToLowerInvariant());
            });
        });
        return services.BuildServiceProvider();
    }

    private static async Task<EchoResponse> RequestWithRetryAsync(IRequester<EchoRequest> requester, string value,
        TimeSpan overallTimeout)
    {
        var deadline = DateTime.UtcNow + overallTimeout;
        Exception last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await requester.RequestAsync<EchoResponse>(new EchoRequest { Value = value },
                    c => c.Timeout = TimeSpan.FromSeconds(3));
                return response.Message;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(200);
            }
        }

        throw new TimeoutException($"Request never succeeded within {overallTimeout}.", last);
    }

    private static async Task AwaitCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task A_server_can_be_stopped_and_started_again_several_times()
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 4222))
            return; // No local NATS container reachable — skipped, not failed.

        var streamName = $"FXLINK_IT_{Guid.NewGuid():N}".ToUpperInvariant()[..20];
        await using var provider = BuildProvider(streamName);
        var connector = provider.GetRequiredService<IMessageBrokerConnector>();
        var requester = provider.GetRequiredService<IRequester<EchoRequest>>();

        for (var round = 1; round <= 3; round++)
        {
            using var cts = new CancellationTokenSource();
            var startTask = connector.StartAsync(cts.Token);

            var response = await RequestWithRetryAsync(requester, $"round-{round}", TimeSpan.FromSeconds(15));
            response.Value.ShouldBe($"round-{round}");

            await cts.CancelAsync();
            await connector.StopAsync();
            await AwaitCancellationAsync(startTask);
        }
    }
}
