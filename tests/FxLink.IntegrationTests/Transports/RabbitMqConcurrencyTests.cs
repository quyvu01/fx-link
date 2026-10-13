using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.IntegrationTests.Fixtures;
using FxLink.IntegrationTests.Support;
using FxLink.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Transports;

// docs/lessons-from-fxmap.md section 6: AsyncEventingBasicConsumer.ReceivedAsync's ea.Body memory
// belongs to the consumer and is reused once the handler returns — completing a
// TaskCompletionSource with a ReadOnlyMemory taken from it and reading it back later (outside the
// handler) can read a DIFFERENT message's bytes under load. Reading RabbitMqClientConnector.cs
// confirmed args.Body.Span is decoded to a string synchronously inside the awaited handler (so the
// memory is still valid) — this is the concurrency test the doc says to write to prove it, not a
// fix: many concurrent requests with distinct payloads, each must get back its own answer.
public class RabbitMqConcurrencyTests
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

    [Fact]
    public async Task Many_concurrent_requests_each_get_their_own_answer_back()
    {
        if (!await LocalBrokers.IsReachableAsync("localhost", 5672))
            return; // No local RabbitMQ container reachable — skipped, not failed.

        await using var provider = BuildProvider();
        var connector = provider.GetRequiredService<IMessageBrokerConnector>();
        var requester = provider.GetRequiredService<IRequester<EchoRequest>>();

        using var cts = new CancellationTokenSource();
        var startTask = connector.StartAsync(cts.Token);
        try
        {
            // Warm the topology up first (exchange/queue/consumer all declared) so the burst below
            // measures concurrent in-flight handling, not cold-start setup time.
            await WarmUpAsync(requester);

            const int concurrency = 200;
            var tasks = Enumerable.Range(0, concurrency).Select(async i =>
            {
                var value = $"payload-{i}";
                var response = await requester.RequestAsync<EchoResponse>(new EchoRequest { Value = value },
                    c => c.Timeout = TimeSpan.FromSeconds(15));
                return (Sent: value, Got: response.Message.Value);
            });

            var results = await Task.WhenAll(tasks);

            // Every single one of the 200 concurrent requests must get back EXACTLY its own
            // payload — a reused/stale ea.Body would surface here as Got != Sent for some pairs.
            foreach (var (sent, got) in results)
                got.ShouldBe(sent);
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

    private static async Task WarmUpAsync(IRequester<EchoRequest> requester)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await requester.RequestAsync<EchoResponse>(new EchoRequest { Value = "warmup" },
                    c => c.Timeout = TimeSpan.FromSeconds(3));
                return;
            }
            catch
            {
                await Task.Delay(200);
            }
        }
    }
}
