using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// Against the real local RabbitMQ container (see tests/FxLink.IntegrationTests/README.md).
// Ported from FxMap's test/FxMap.Tests/TransportTests/ServerLifecycleTests.cs
// (*_a_server_can_be_stopped_and_started_again, see docs/lessons-from-fxmap.md section 3): build
// the owner without starting a host, pull IMessageBrokerConnector straight out of the container,
// then three times in a row: StartAsync in the background, prove a request is answered, cancel,
// StopAsync. FxMap's version of this test is what caught the SQS "two pollers on the same queue"
// bug this round — this is RabbitMqClient's counterpart. RabbitMqClient wasn't found to have that
// bug (see the SqsClient fix in this change), so this test is here to pin the behavior down, not
// to reproduce a known failure.
public class RabbitMqLifecycleTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddRabbitMq(c => c.Host("localhost", "/"));
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
        if (!await LocalBrokers.IsReachableAsync("localhost", 5672))
            return; // No local RabbitMQ container reachable — skipped, not failed.

        await using var provider = BuildProvider();
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
