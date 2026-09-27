using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Azure.ServiceBus.Extensions;
using FxLink.Azure.ServiceBus.Topology;
using FxLink.Contexts;
using FxLink.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Azure.ServiceBus.Tests;

public class ServiceBusTopologyExporterTests
{
    public sealed class OrderPlaced;

    public sealed class FirstConsumer : IConsumer<OrderPlaced>
    {
        public Task ConsumeAsync(IConsumeContext<OrderPlaced> context, CancellationToken token = default) =>
            Task.CompletedTask;
    }

    public sealed class SecondConsumer : IConsumer<OrderPlaced>
    {
        public Task ConsumeAsync(IConsumeContext<OrderPlaced> context, CancellationToken token = default) =>
            Task.CompletedTask;
    }

    private static JsonElement Export()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxLink(options =>
        {
            options.AddConsumer<FirstConsumer>();
            options.AddConsumer<SecondConsumer>();
            options.AddServiceBus(bus => bus.ConnectionString("Endpoint=sb://localhost;SharedAccessKeyName=k;SharedAccessKey=v"));
        });
        using var provider = services.BuildServiceProvider();
        var json = provider.GetRequiredService<IServiceBusTopologyExporter>().ToEmulatorConfigJson("ns");
        return JsonDocument.Parse(json).RootElement.GetProperty("UserConfig").GetProperty("Namespaces")[0].Clone();
    }

    [Fact]
    public void Each_message_type_becomes_a_topic_with_a_subscription_per_consumer()
    {
        var ns = Export();

        ns.GetProperty("Name").GetString().ShouldBe("ns");
        var topic = ns.GetProperty("Topics").EnumerateArray().Single();
        topic.GetProperty("Name").GetString().ShouldBe(typeof(OrderPlaced).GetTopicName());
        topic.GetProperty("Subscriptions").EnumerateArray().Select(s => s.GetProperty("Name").GetString())
            .ShouldBe([typeof(FirstConsumer).GetSubscriptionName(), typeof(SecondConsumer).GetSubscriptionName()],
                ignoreOrder: true);
    }

    [Fact]
    public void Every_subscription_carries_the_mandatory_targeting_rule()
    {
        var topic = Export().GetProperty("Topics").EnumerateArray().Single();

        foreach (var subscription in topic.GetProperty("Subscriptions").EnumerateArray())
        {
            var rule = subscription.GetProperty("Rules").EnumerateArray().Single();
            rule.GetProperty("Properties").GetProperty("SqlFilter").GetProperty("SqlExpression").GetString()
                .ShouldBe(ServiceBusTopology.TargetRule(subscription.GetProperty("Name").GetString()));
        }
    }

    [Fact]
    public void The_reply_queue_requires_sessions()
    {
        var queue = Export().GetProperty("Queues").EnumerateArray().Single();

        queue.GetProperty("Name").GetString().ShouldBe("fxlink-replies");
        queue.GetProperty("Properties").GetProperty("RequiresSession").GetBoolean().ShouldBeTrue();
    }
}
