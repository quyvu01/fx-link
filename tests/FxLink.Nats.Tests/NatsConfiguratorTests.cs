using FxLink.Nats.Registries;
using Shouldly;
using Xunit;

namespace FxLink.Nats.Tests;

public class NatsConfiguratorTests
{
    [Fact]
    public void ToConfiguration_throws_when_no_server_was_configured()
    {
        Should.Throw<InvalidOperationException>(() => new NatsConfigurator().ToConfiguration());
    }

    [Fact]
    public void Defaults_are_applied_when_only_the_server_is_configured()
    {
        var configurator = new NatsConfigurator();
        configurator.Server("nats://localhost:4222");

        var configuration = configurator.ToConfiguration();

        configuration.Url.ShouldBe("nats://localhost:4222");
        configuration.StreamName.ShouldBe("FXLINK");
        configuration.SubjectPrefix.ShouldBe("fxlink");
        configuration.AutoProvision.ShouldBeTrue();
        configuration.MaxDeliver.ShouldBe(5);
        configuration.AckWait.ShouldBe(TimeSpan.FromSeconds(30));
        configuration.MaxAckPending.ShouldBe(100);
        configuration.MaxAge.ShouldBe(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Credential_callback_values_reach_the_configuration()
    {
        var configurator = new NatsConfigurator();
        configurator.Server("nats://h", c =>
        {
            c.UserName("u");
            c.Password("p");
            c.Token("t");
            c.CredsFile("/x.creds");
        });

        var configuration = configurator.ToConfiguration();

        configuration.UserName.ShouldBe("u");
        configuration.Password.ShouldBe("p");
        configuration.Token.ShouldBe("t");
        configuration.CredsFile.ShouldBe("/x.creds");
    }

    [Theory]
    [InlineData("a.b")]
    [InlineData("a*")]
    [InlineData("a>")]
    [InlineData("a b")]
    public void SubjectPrefix_must_be_a_single_token(string prefix)
    {
        Should.Throw<ArgumentException>(() => new NatsConfigurator().SubjectPrefix(prefix));
    }

    [Fact]
    public void Numeric_options_reject_non_positive_values()
    {
        var configurator = new NatsConfigurator();

        Should.Throw<ArgumentOutOfRangeException>(() => configurator.MaxDeliver(0));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.MaxAckPending(0));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.AckWait(TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.MaxAge(TimeSpan.Zero));
    }
}
