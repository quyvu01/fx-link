using FxLink.Azure.ServiceBus.Extensions;
using FxLink.Azure.ServiceBus.Topology;
using Shouldly;
using Xunit;

namespace FxLink.Azure.ServiceBus.Tests;

public class ServiceBusNamingTests
{
    private sealed class Sample;

    private sealed class Wrapper<T>;

    [Fact]
    public void Topic_and_subscription_names_are_lowercase_and_dotted()
    {
        typeof(Sample).GetTopicName().ShouldBe("fxlink.azure.servicebus.tests.sample");
        typeof(Sample).GetSubscriptionName().ShouldBe("fxlink.azure.servicebus.tests.sample");
    }

    [Fact]
    public void Closed_generics_get_distinct_names()
    {
        typeof(Wrapper<int>).GetTopicName().ShouldNotBe(typeof(Wrapper<string>).GetTopicName());
    }

    [Fact]
    public void Illegal_characters_are_replaced()
    {
        ServiceBusNamingExtensions.SanitizeTopic("Orders Created*").ShouldBe("orders-created-");
    }

    [Fact]
    public void Subscription_names_within_the_limit_are_left_alone()
    {
        ServiceBusNamingExtensions.SanitizeSubscription(new string('a', 50)).ShouldBe(new string('a', 50));
    }

    [Fact]
    public void Long_subscription_names_are_shortened_to_the_limit_with_a_stable_hash()
    {
        var name = "company.product.module." + new string('x', 60);

        var shortened = ServiceBusNamingExtensions.SanitizeSubscription(name);

        shortened.Length.ShouldBe(50);
        ServiceBusNamingExtensions.SanitizeSubscription(name).ShouldBe(shortened);
    }

    [Fact]
    public void Long_names_sharing_a_prefix_do_not_collide()
    {
        var prefix = "company.product.module." + new string('x', 40);

        ServiceBusNamingExtensions.SanitizeSubscription(prefix + "OrderCreated")
            .ShouldNotBe(ServiceBusNamingExtensions.SanitizeSubscription(prefix + "OrderCancelled"));
    }

    [Fact]
    public void Target_rule_matches_untargeted_messages_and_the_named_subscription()
    {
        ServiceBusTopology.TargetRule("orders")
            .ShouldBe("TargetSubscription IS NULL OR TargetSubscription = 'orders'");
    }

    [Fact]
    public void Target_rule_escapes_single_quotes()
    {
        ServiceBusTopology.TargetRule("o'brien")
            .ShouldBe("TargetSubscription IS NULL OR TargetSubscription = 'o''brien'");
    }
}
