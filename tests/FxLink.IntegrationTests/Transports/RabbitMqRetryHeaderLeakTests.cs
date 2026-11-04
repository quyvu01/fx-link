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

// A message that failed once and was retried carries retry headers. A message the consumer publishes
// while handling that retried delivery must not inherit them (it used to be routed to the retry
// exchange of its own type, where nothing ever delivers it). Own throwaway vhost per test.
public class RabbitMqRetryHeaderLeakTests
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
            opts.AddConsumersFromAssemblies(typeof(RetryLeakTriggerConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(RetryLeakTriggerConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(RetryLeakTriggerConsumer).Assembly);
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

        var vhost = $"fxlink-retryleak-{Guid.NewGuid():N}";
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
                    return queues.Contains(nameof(RetryLeakTriggerConsumer), StringComparison.OrdinalIgnoreCase);
                }, TimeSpan.FromSeconds(20), pollMs: 200);

                var id = Guid.NewGuid();
                await publisher.PublishAsync(new RetryLeakTrigger { Id = id });
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
    public Task A_message_published_while_handling_a_retried_delivery_is_delivered_without_retry_headers() =>
        RunAsync(null, async id =>
        {
            // 2s default first retry interval, then the follow-up must arrive on the main exchange.
            await Polling.WaitUntilAsync(() => RetryLeakFollowUpConsumer.Received.ContainsKey(id),
                TimeSpan.FromSeconds(30));
            var (deliveryKind, retryCount) = RetryLeakFollowUpConsumer.Received[id];
            deliveryKind.ShouldBeNull();
            retryCount.ShouldBe(0);
            RetryLeakTriggerConsumer.Attempts[id].ShouldBe(2);
        });
}
