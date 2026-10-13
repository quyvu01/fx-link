using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// This is the FxMap bug the lessons doc is named after (docs/lessons-from-fxmap.md section 2):
// a server-named reply queue (broker picks the name) comes back with a DIFFERENT name after
// AutomaticRecoveryEnabled reconnects, while in-flight requests still carry the old name in
// ReplyTo — every request timed out until the process restarted. FxLink's RabbitMqClient already
// has a QueueNameChangedAfterRecoveryAsync handler that updates ReplyQueueName (confirmed by
// reading the source — see the lessons doc's note that this "can be right, but nothing shows it").
// This test is that missing proof: cut the connection via BrokerProxy, let AutomaticRecoveryEnabled
// reconnect, and confirm a request still gets answered afterward instead of hanging forever.
public class RabbitMqOutageTests
{
    private const int ProxyPort = 25672; // distinct from RabbitMQ's own 5672/15672/25672-AMQP-cluster port usage on this box

    private static ServiceProvider BuildProvider(int port)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddRabbitMq(c => c.Host("localhost", "/", port));
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
                // A longer per-attempt timeout and a longer gap between attempts than the other
                // lifecycle tests use on purpose: while the connection is down, a publish can sit
                // on a dead channel for a while before RabbitMqClient notices and replaces it (see
                // PublishMessageAsync's IsOpen check) — retrying too aggressively would just pile up
                // more concurrently-stuck attempts against the bounded publish channel pool instead
                // of giving any one of them room to fail and get replaced.
                var response = await requester.RequestAsync<EchoResponse>(new EchoRequest { Value = value },
                    c => c.Timeout = TimeSpan.FromSeconds(5));
                return response.Message;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(2000);
            }
        }

        throw new TimeoutException($"Request never succeeded within {overallTimeout}.", last);
    }

    [Fact]
    public async Task A_request_succeeds_again_after_the_connection_is_cut_and_restored()
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 5672))
            return; // No local RabbitMQ container reachable — skipped, not failed.

        // RabbitMqClient.AutomaticRecoveryEnabled uses NetworkRecoveryInterval = 10s — the proxy
        // needs to stay cut at least that long for a reconnect attempt to actually happen against
        // it, and the final request budget needs to comfortably clear one such interval.
        await using var proxy = new BrokerProxy("localhost", 5672, ProxyPort);
        await proxy.StartAsync();

        await using var provider = BuildProvider(ProxyPort);
        var connector = provider.GetRequiredService<IMessageBrokerConnector>();
        var requester = provider.GetRequiredService<IRequester<EchoRequest>>();

        using var cts = new CancellationTokenSource();
        var startTask = connector.StartAsync(cts.Token);
        try
        {
            var before = await RequestWithRetryAsync(requester, "before-outage", TimeSpan.FromSeconds(15));
            before.Value.ShouldBe("before-outage");

            proxy.Cut();
            await Task.Delay(TimeSpan.FromSeconds(3));
            proxy.Restore();

            // AutomaticRecoveryEnabled reconnects on its own NetworkRecoveryInterval (10s); this
            // retries requests across that whole window rather than asserting a specific recovery
            // time, same as the lessons doc's own "~recovery interval" expectation for FxMap.
            var after = await RequestWithRetryAsync(requester, "after-outage", TimeSpan.FromSeconds(60));
            after.Value.ShouldBe("after-outage");
        }
        finally
        {
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
    }
}
