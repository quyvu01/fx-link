using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// IRabbitMqConfigurator.Durable(bool): every test runs in its own throwaway vhost (created and
// deleted through the management API), because RabbitMQ refuses to re-declare an existing queue or
// exchange with a different durability — the queues the other tests in this project leave behind in
// "/" are non-durable and would make a durable declare fail with PRECONDITION_FAILED.
public class RabbitMqDurableTests
{
    private const string ManagementUrl = "http://localhost:15672";

    private static HttpClient CreateManagementClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(ManagementUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
        return client;
    }

    private static ServiceProvider BuildProvider(string vhost, bool? durable)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(EchoConsumer).Assembly);
            opts.AddRabbitMq(c =>
            {
                c.Host("localhost", vhost);
                if (durable is { } value) c.Durable(value);
            });
        });
        return services.BuildServiceProvider();
    }

    private static async Task<JsonElement[]> GetArrayAsync(HttpClient http, string path)
    {
        using var response = await http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return [.. doc.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    private static async Task RunAsync(bool? durable, bool expectedDurable)
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 5672) ||
            !await LocalBrokers.IsReachableAsync("localhost", 15672))
            return; // RabbitMQ with the management plugin isn't reachable — skipped, not failed.

        var vhost = $"fxlink-durable-{Guid.NewGuid():N}";
        var escaped = Uri.EscapeDataString(vhost);
        using var http = CreateManagementClient();
        (await http.PutAsync($"/api/vhosts/{escaped}", new StringContent("{}", Encoding.UTF8, "application/json")))
            .EnsureSuccessStatusCode();
        // guest only gets access to a new vhost if it's granted explicitly.
        (await http.PutAsync($"/api/permissions/{escaped}/guest",
                new StringContent("""{"configure":".*","write":".*","read":".*"}""", Encoding.UTF8,
                    "application/json")))
            .EnsureSuccessStatusCode();

        try
        {
            await using var provider = BuildProvider(vhost, durable);
            var connector = provider.GetRequiredService<IMessageBrokerConnector>();
            var requester = provider.GetRequiredService<IRequester<EchoRequest>>();

            using var cts = new CancellationTokenSource();
            var startTask = connector.StartAsync(cts.Token);
            try
            {
                // A full round trip proves the topology declares succeeded and that persistent
                // publishing doesn't break request/response (responses are deliberately not persistent).
                EchoResponse response = null;
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
                while (response is null)
                {
                    try
                    {
                        response = (await requester.RequestAsync<EchoResponse>(new EchoRequest { Value = "ping" },
                            c => c.Timeout = TimeSpan.FromSeconds(3))).Message;
                    }
                    catch when (DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(200);
                    }
                }

                response.Value.ShouldBe("ping");

                var queues = await GetArrayAsync(http, $"/api/queues/{escaped}");
                // The per-instance reply queue is exclusive and always non-durable.
                var topologyQueues = queues.Where(q => !q.GetProperty("exclusive").GetBoolean()).ToArray();
                topologyQueues.ShouldNotBeEmpty();
                topologyQueues.ShouldAllBe(q => q.GetProperty("durable").GetBoolean() == expectedDurable);

                var exchanges = await GetArrayAsync(http, $"/api/exchanges/{escaped}");
                var topologyExchanges = exchanges.Where(e =>
                {
                    var name = e.GetProperty("name").GetString();
                    return !string.IsNullOrEmpty(name) && !name.StartsWith("amq.", StringComparison.Ordinal);
                }).ToArray();
                topologyExchanges.ShouldNotBeEmpty();
                topologyExchanges.ShouldAllBe(e => e.GetProperty("durable").GetBoolean() == expectedDurable);
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
        finally
        {
            await http.DeleteAsync($"/api/vhosts/{escaped}");
        }
    }

    [Fact]
    public Task Topology_is_durable_when_Durable_true_is_configured() => RunAsync(durable: true, expectedDurable: true);

    [Fact]
    public Task Topology_is_not_durable_by_default() => RunAsync(durable: null, expectedDurable: false);

    [Fact]
    public Task Topology_is_not_durable_when_Durable_false_is_configured() =>
        RunAsync(durable: false, expectedDurable: false);
}
