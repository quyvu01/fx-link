using FxLink.Azure.ServiceBus.Registries;
using Shouldly;
using Xunit;

namespace FxLink.Azure.ServiceBus.Tests;

public class ServiceBusConfiguratorTests
{
    private const string ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=k;SharedAccessKey=v";

    [Fact]
    public void ToConfiguration_throws_when_no_connection_was_configured()
    {
        Should.Throw<InvalidOperationException>(() => new ServiceBusConfigurator().ToConfiguration());
    }

    [Fact]
    public void ToConfiguration_throws_when_both_a_connection_string_and_a_namespace_were_configured()
    {
        var configurator = new ServiceBusConfigurator();
        configurator.ConnectionString(ConnectionString);
        configurator.Namespace("x.servicebus.windows.net");

        Should.Throw<InvalidOperationException>(configurator.ToConfiguration);
    }

    [Fact]
    public void Defaults_are_applied_when_only_a_connection_string_is_configured()
    {
        var configurator = new ServiceBusConfigurator();
        configurator.ConnectionString(ConnectionString);

        var configuration = configurator.ToConfiguration();

        configuration.ConnectionString.ShouldBe(ConnectionString);
        configuration.AutoProvision.ShouldBeTrue();
        configuration.ReplyQueueName.ShouldBe("fxlink-replies");
        configuration.MaxDeliveryCount.ShouldBe(5);
        configuration.LockDuration.ShouldBe(TimeSpan.FromSeconds(30));
        configuration.MaxConcurrentCalls.ShouldBe(8);
        configuration.PrefetchCount.ShouldBe(0);
        configuration.MaxAutoLockRenewalDuration.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Namespace_configuration_keeps_the_namespace_and_leaves_the_connection_string_empty()
    {
        var configurator = new ServiceBusConfigurator();
        configurator.Namespace("x.servicebus.windows.net");

        var configuration = configurator.ToConfiguration();

        configuration.FullyQualifiedNamespace.ShouldBe("x.servicebus.windows.net");
        configuration.ConnectionString.ShouldBeNull();
    }

    [Fact]
    public void Options_reject_out_of_range_values()
    {
        var configurator = new ServiceBusConfigurator();

        Should.Throw<ArgumentOutOfRangeException>(() => configurator.MaxDeliveryCount(0));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.MaxConcurrentCalls(0));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.PrefetchCount(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.LockDuration(TimeSpan.FromSeconds(4)));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.LockDuration(TimeSpan.FromMinutes(6)));
    }
}
