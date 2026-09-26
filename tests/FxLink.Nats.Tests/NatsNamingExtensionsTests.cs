using FxLink.Nats.Extensions;
using Shouldly;
using Xunit;

namespace FxLink.Nats.Tests;

public class NatsNamingExtensionsTests
{
    private sealed class Sample;

    private sealed class Wrapper<T>;

    [Fact]
    public void Subject_names_keep_dots_as_hierarchy_and_are_lowercase()
    {
        typeof(Sample).GetSubjectName().ShouldBe("fxlink.nats.tests.sample");
    }

    [Fact]
    public void Consumer_names_replace_dots_since_durable_names_cannot_contain_them()
    {
        typeof(Sample).GetConsumerName().ShouldBe("fxlink-nats-tests-sample");
    }

    [Fact]
    public void Closed_generics_get_distinct_names()
    {
        typeof(Wrapper<int>).GetSubjectName().ShouldNotBe(typeof(Wrapper<string>).GetSubjectName());
    }

    [Theory]
    [InlineData("Orders.Created", "orders.created")]
    [InlineData("a b*c>d", "a-b-c-d")]
    public void SanitizeSubject_removes_wildcards_and_whitespace(string input, string expected)
    {
        NatsNamingExtensions.SanitizeSubject(input).ShouldBe(expected);
    }

    [Fact]
    public void SanitizeConsumer_also_removes_dots()
    {
        NatsNamingExtensions.SanitizeConsumer("Orders.Created").ShouldBe("orders-created");
    }

    [Fact]
    public void Dead_letter_consumer_name_is_derived_from_the_consumer_name()
    {
        "orders-consumer".DeadLetterConsumerName().ShouldBe("orders-consumer-deadletter");
    }
}
