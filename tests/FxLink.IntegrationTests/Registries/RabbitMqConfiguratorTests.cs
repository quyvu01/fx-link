using FxLink.RabbitMq.Registries;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace FxLink.IntegrationTests.Registries;

// No broker needed: these only check how IRabbitMqConfigurator's TLS/connection options resolve into
// IRabbitMqConfiguration. Lives here rather than in a separate unit-test project because this is
// already the only test project referencing FxLink.RabbitMq.
public class RabbitMqConfiguratorTests
{
    private static IRabbitMqConfiguration Build(Action<RabbitMqConfigurator> configure)
    {
        var configurator = new RabbitMqConfigurator();
        configure(configurator);
        return configurator.ToConfiguration();
    }

    [Fact]
    public void Without_Ssl_TLS_is_off_and_the_port_defaults_to_5672()
    {
        var configuration = Build(c => c.Host("rabbit.internal", "/"));

        configuration.SslOption.ShouldBeNull();
        configuration.RabbitMqPort.ShouldBe(5672);
    }

    [Fact]
    public void Calling_Ssl_enables_TLS_even_when_the_callback_does_not_set_Enabled()
    {
        var configuration = Build(c => c.Host("rabbit.internal", "/",
            configure: credential => credential.Ssl(o => o.CertPath = "/certs/client.p12")));

        configuration.SslOption.Enabled.ShouldBeTrue();
        configuration.SslOption.CertPath.ShouldBe("/certs/client.p12");
    }

    [Fact]
    public void Ssl_ServerName_defaults_to_the_host_and_the_port_to_5671()
    {
        var configuration = Build(c => c.Host("rabbit.internal", "/", configure: credential => credential.Ssl()));

        configuration.SslOption.ServerName.ShouldBe("rabbit.internal");
        configuration.RabbitMqPort.ShouldBe(5671);
    }

    [Fact]
    public void An_explicit_ServerName_and_port_are_kept()
    {
        var configuration = Build(c => c.Host("10.0.0.5", "/", 15671,
            credential => credential.Ssl(o => o.ServerName = "rabbit.prod")));

        configuration.SslOption.ServerName.ShouldBe("rabbit.prod");
        configuration.RabbitMqPort.ShouldBe(15671);
    }

    [Fact]
    public void The_callback_can_still_turn_TLS_off()
    {
        var configuration = Build(c => c.Host("rabbit.internal", "/",
            configure: credential => credential.Ssl(o => o.Enabled = false)));

        configuration.SslOption.Enabled.ShouldBeFalse();
        configuration.RabbitMqPort.ShouldBe(5672);
    }

    [Fact]
    public void ConfigureConnection_callbacks_are_applied_in_order()
    {
        var configuration = Build(c =>
        {
            c.Host("rabbit.internal", "/");
            c.ConfigureConnection(f => f.ClientProvidedName = "first");
            c.ConfigureConnection(f => f.ClientProvidedName += "-second");
        });

        var factory = new ConnectionFactory();
        configuration.ConfigureConnection.ShouldNotBeNull();
        configuration.ConfigureConnection(factory);
        factory.ClientProvidedName.ShouldBe("first-second");
    }

    [Fact]
    public void ConfigureConnection_is_null_when_not_configured() =>
        Build(c => c.Host("rabbit.internal", "/")).ConfigureConnection.ShouldBeNull();
}
