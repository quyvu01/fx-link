using FxLink.Outbox.EntityFrameworkCore.Exceptions;
using FxLink.Outbox.EntityFrameworkCore.Registries;
using FxLink.Outbox.EntityFrameworkCore.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Outbox.EntityFrameworkCore.Tests.Registries;

public class OutboxEntityFrameworkConfiguratorTests
{
    [Fact]
    public void ValidateItSelf_throws_DbContextNotConfigured_when_never_configured()
    {
        var configurator = new OutboxEntityFrameworkConfigurator(new ServiceCollection(), registrationKey: null);

        Should.Throw<OutboxEntityFrameworkCoreException.DbContextNotConfigured>(configurator.ValidateItSelf);
    }

    [Fact]
    public void ValidateItSelf_does_not_throw_when_AddDbContext_is_called_exactly_once()
    {
        var configurator = new OutboxEntityFrameworkConfigurator(new ServiceCollection(), registrationKey: null);

        configurator.AddDbContext<TestOutboxDbContext>();

        Should.NotThrow(configurator.ValidateItSelf);
    }

    [Fact]
    public void ValidateItSelf_does_not_throw_when_DbContextFactory_is_called_exactly_once()
    {
        var configurator = new OutboxEntityFrameworkConfigurator(new ServiceCollection(), registrationKey: null);

        configurator.DbContextFactory(_ => (TestOutboxDbContext)null!);

        Should.NotThrow(configurator.ValidateItSelf);
    }

    [Fact]
    public void ValidateItSelf_throws_DbContextAlreadyConfigured_when_configured_more_than_once()
    {
        var configurator = new OutboxEntityFrameworkConfigurator(new ServiceCollection(), registrationKey: null);

        configurator.AddDbContext<TestOutboxDbContext>();
        configurator.DbContextFactory(() => (TestOutboxDbContext)null!);

        Should.Throw<OutboxEntityFrameworkCoreException.DbContextAlreadyConfigured>(configurator.ValidateItSelf);
    }
}
