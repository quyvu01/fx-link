using System.Net.Http.Headers;
using System.Text;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.RabbitMq.Extensions;
using FxLink.RabbitMq.Registries;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// IRabbitMqConfigurator.RequeueOnFailure: an exception escaping the consumer pipeline used to be
// acked (message lost). Each test runs in its own throwaway vhost so other test classes — which
// register RequeueProbeConsumer too via assembly scanning, but without FailFirstDeliveryBehavior —
// can't consume these probes from the shared queue.
public class RabbitMqRequeueOnFailureTests
{
    private static HttpClient CreateManagementClient()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:15672") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
        return client;
    }

    private static ServiceProvider BuildProvider(string vhost, Action<IRabbitMqConfigurator> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddConsumerPipelineBehaviors(c => c.Of<FailFirstDeliveryBehavior>());
            opts.AddRabbitMq(c =>
            {
                c.Host("localhost", vhost);
                configure?.Invoke(c);
            });
        });
        return services.BuildServiceProvider();
    }

    private static async Task RunAsync(Action<IRabbitMqConfigurator> configure, Func<Guid, Task> assert)
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 5672) ||
            !await LocalBrokers.IsReachableAsync("localhost", 15672))
            return; // RabbitMQ with the management plugin isn't reachable — skipped, not failed.

        var vhost = $"fxlink-requeue-{Guid.NewGuid():N}";
        var escaped = Uri.EscapeDataString(vhost);
        using var http = CreateManagementClient();
        (await http.PutAsync($"/api/vhosts/{escaped}", new StringContent("{}", Encoding.UTF8, "application/json")))
            .EnsureSuccessStatusCode();
        (await http.PutAsync($"/api/permissions/{escaped}/guest",
                new StringContent("""{"configure":".*","write":".*","read":".*"}""", Encoding.UTF8,
                    "application/json")))
            .EnsureSuccessStatusCode();

        try
        {
            await using var provider = BuildProvider(vhost, configure);
            var connector = provider.GetRequiredService<IMessageBrokerConnector>();
            var publisher = provider.GetRequiredService<IPublisher>();

            using var cts = new CancellationTokenSource();
            var startTask = connector.StartAsync(cts.Token);
            try
            {
                // Publishing before the consumer's queue is bound would drop the probe as unroutable,
                // so wait until the queue exists in this vhost first.
                await Polling.WaitUntilAsync(() =>
                {
                    var queues = http.GetStringAsync($"/api/queues/{escaped}").GetAwaiter().GetResult();
                    return queues.Contains(nameof(RequeueProbeConsumer), StringComparison.OrdinalIgnoreCase);
                }, TimeSpan.FromSeconds(20), pollMs: 200);

                var id = Guid.NewGuid();
                await publisher.PublishAsync(new RequeueProbe { Id = id });
                await assert(id);
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
        finally
        {
            await http.DeleteAsync($"/api/vhosts/{escaped}");
        }
    }

    [Fact]
    public Task A_delivery_whose_pipeline_throws_is_redelivered_when_RequeueOnFailure_is_configured() =>
        RunAsync(c => c.RequeueOnFailure(TimeSpan.FromMilliseconds(500)), async id =>
        {
            await Polling.WaitUntilAsync(() => RequeueProbeConsumer.Received.Contains(id), TimeSpan.FromSeconds(20));
            FailFirstDeliveryBehavior.Attempts[id].ShouldBe(2);
        });

    [Fact]
    public Task A_delivery_whose_pipeline_throws_is_acked_and_lost_by_default() =>
        RunAsync(null, async id =>
        {
            await Polling.WaitUntilAsync(() => FailFirstDeliveryBehavior.Attempts.ContainsKey(id),
                TimeSpan.FromSeconds(20));
            await Task.Delay(TimeSpan.FromSeconds(3));
            FailFirstDeliveryBehavior.Attempts[id].ShouldBe(1);
            RequeueProbeConsumer.Received.ShouldNotContain(id);
        });
}
