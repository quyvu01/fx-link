using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// A delivery that doesn't come through its message type's exchange (here: sent straight to the queue
// via the default exchange) is resolved from its Type property — only against the message types this
// process consumes, never via Type.GetType. Own throwaway vhost per test, like RabbitMqRequeueOnFailureTests.
public class RabbitMqMessageTypeResolutionTests
{
    private static HttpClient CreateManagementClient()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:15672") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")));
        return client;
    }

    private static ServiceProvider BuildProvider(string vhost)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(opts =>
        {
            opts.AddConsumersFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddConsumerDefinitionsFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddMessageDefinitionsFromAssemblies(typeof(RequeueProbeConsumer).Assembly);
            opts.AddRabbitMq(c => c.Host("localhost", vhost));
        });
        return services.BuildServiceProvider();
    }

    private static string ProbeBody(Guid id) => $$"""
        {"message":{"id":"{{id}}"},"context":{"messageId":"{{Guid.NewGuid()}}","correlationId":"{{Guid.NewGuid()}}","headers":{}}}
        """;

    private static async Task RunAsync(Func<Func<string, Guid, Task>, Task> body)
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 5672) ||
            !await LocalBrokers.IsReachableAsync("localhost", 15672))
            return; // RabbitMQ with the management plugin isn't reachable — skipped, not failed.

        var vhost = $"fxlink-typeres-{Guid.NewGuid():N}";
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
            await using var provider = BuildProvider(vhost);
            var connector = provider.GetRequiredService<IMessageBrokerConnector>();

            using var cts = new CancellationTokenSource();
            var startTask = connector.StartAsync(cts.Token);
            try
            {
                string queueName = null;
                await Polling.WaitUntilAsync(() =>
                {
                    using var queues = JsonDocument.Parse(http.GetStringAsync($"/api/queues/{escaped}")
                        .GetAwaiter().GetResult());
                    queueName = queues.RootElement.EnumerateArray()
                        .Select(q => q.GetProperty("name").GetString())
                        .Where(n => n!.Contains(nameof(RequeueProbeConsumer), StringComparison.OrdinalIgnoreCase))
                        .OrderBy(n => n!.Length)
                        .FirstOrDefault();
                    return queueName is not null;
                }, TimeSpan.FromSeconds(20), pollMs: 200);

                var factory = new ConnectionFactory { HostName = "localhost", VirtualHost = vhost };
                await using var connection = await factory.CreateConnectionAsync();
                await using var channel = await connection.CreateChannelAsync();

                await body(async (typeName, id) =>
                {
                    var props = new BasicProperties { Type = typeName };
                    await channel.BasicPublishAsync(string.Empty, queueName!, mandatory: true, props,
                        Encoding.UTF8.GetBytes(ProbeBody(id)));
                });
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
    public Task A_type_name_from_a_different_contracts_version_is_still_consumed() =>
        RunAsync(async send =>
        {
            var id = Guid.NewGuid();
            var typeName = typeof(RequeueProbe).AssemblyQualifiedName!
                .Replace(typeof(RequeueProbe).Assembly.GetName().Version!.ToString(), "9.8.7.6");

            await send(typeName, id);

            await Polling.WaitUntilAsync(() => RequeueProbeConsumer.Received.Contains(id), TimeSpan.FromSeconds(20));
        });

    [Fact]
    public Task A_type_name_outside_the_consumed_message_types_is_dropped_without_stalling_the_consumer() =>
        RunAsync(async send =>
        {
            var dropped = Guid.NewGuid();
            await send("System.Diagnostics.Process, System.Diagnostics.Process", dropped);

            // The unknown delivery is acked and dropped; a valid one sent after it still gets through.
            var delivered = Guid.NewGuid();
            await send(typeof(RequeueProbe).AssemblyQualifiedName, delivered);

            await Polling.WaitUntilAsync(() => RequeueProbeConsumer.Received.Contains(delivered),
                TimeSpan.FromSeconds(20));
            RequeueProbeConsumer.Received.ShouldNotContain(dropped);
        });
}
