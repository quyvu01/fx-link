using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

// IRabbitMqConfigurator.PublisherConfirms: publish channels are opened in confirm mode by default, so a
// publish only returns once the broker has acked it. Each test runs in its own throwaway vhost (see
// RabbitMqRequeueOnFailureTests) so the management API's channel list only shows this test's channels.
public class RabbitMqPublisherConfirmsTests
{
    private const int PoolSize = 2;

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
            opts.AddConsumersFromAssemblies(typeof(CollectorConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(CollectorConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(CollectorConsumer).Assembly);
            opts.AddRabbitMq(c =>
            {
                c.Host("localhost", vhost);
                c.PublishChannelPoolSize(PoolSize);
                configure?.Invoke(c);
            });
        });
        return services.BuildServiceProvider();
    }

    private static async Task RunAsync(Action<IRabbitMqConfigurator> configure,
        Func<HttpClient, string, IPublisher, Task> body)
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 5672) ||
            !await LocalBrokers.IsReachableAsync("localhost", 15672))
            return; // RabbitMQ with the management plugin isn't reachable — skipped, not failed.

        var vhost = $"fxlink-confirms-{Guid.NewGuid():N}";
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
                await Polling.WaitUntilAsync(() => QueueNames(http, escaped).Any(IsCollectorQueue),
                    TimeSpan.FromSeconds(20), pollMs: 200);
                await body(http, escaped, publisher);
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

    private static bool IsCollectorQueue(string name) =>
        name.Contains(nameof(CollectorConsumer), StringComparison.OrdinalIgnoreCase);

    private static List<string> QueueNames(HttpClient http, string escapedVhost)
    {
        using var queues = JsonDocument.Parse(http.GetStringAsync($"/api/queues/{escapedVhost}").GetAwaiter().GetResult());
        return queues.RootElement.EnumerateArray().Select(q => q.GetProperty("name").GetString()).ToList();
    }

    // Channel stats reach the management API on its own collection interval, so poll for them.
    private static async Task<int> WaitForConfirmChannelCountAsync(HttpClient http, string escapedVhost,
        Func<int, bool> until)
    {
        var count = -1;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            using var channels = JsonDocument.Parse(await http.GetStringAsync($"/api/vhosts/{escapedVhost}/channels"));
            var list = channels.RootElement.EnumerateArray().ToList();
            count = list.Count(c => c.TryGetProperty("confirm", out var confirm) && confirm.GetBoolean());
            if (list.Count >= PoolSize && until(count)) return count;
            await Task.Delay(500);
        }

        return count;
    }

    [Fact]
    public Task Publish_channels_use_confirm_mode_by_default_and_messages_still_flow() =>
        RunAsync(null, async (http, vhost, publisher) =>
        {
            (await WaitForConfirmChannelCountAsync(http, vhost, n => n >= PoolSize)).ShouldBeGreaterThanOrEqualTo(PoolSize);

            const int sequence = 7_001;
            await publisher.PublishAsync(new CollectedMessage { Sequence = sequence });
            await Polling.WaitUntilAsync(() => CollectorConsumer.Received.Contains(sequence), TimeSpan.FromSeconds(20));
        });

    [Fact]
    public Task Publish_channels_do_not_use_confirm_mode_when_turned_off() =>
        RunAsync(c => c.PublisherConfirms(false), async (http, vhost, _) =>
        {
            // Give the stats a chance to show up before asserting nothing is in confirm mode.
            await Task.Delay(TimeSpan.FromSeconds(6));
            (await WaitForConfirmChannelCountAsync(http, vhost, n => n == 0)).ShouldBe(0);
        });

    [Fact]
    public Task An_unroutable_publish_with_confirms_on_is_logged_not_thrown() =>
        RunAsync(null, async (http, vhost, publisher) =>
        {
            // Delete the only queue bound to CollectedMessage's exchange: the mandatory publish is now
            // returned by the broker, which with confirm tracking surfaces as PublishException.IsReturn.
            foreach (var queue in QueueNames(http, vhost).Where(IsCollectorQueue))
                (await http.DeleteAsync($"/api/queues/{vhost}/{Uri.EscapeDataString(queue)}")).EnsureSuccessStatusCode();

            await Should.NotThrowAsync(() => publisher.PublishAsync(new CollectedMessage { Sequence = 7_002 }));
        });
}
