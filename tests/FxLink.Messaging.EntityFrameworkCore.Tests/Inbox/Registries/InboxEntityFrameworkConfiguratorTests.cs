using FxLink.Messaging.EntityFrameworkCore.Exceptions;
using FxLink.Messaging.EntityFrameworkCore.Inbox.Registries;
using FxLink.Messaging.EntityFrameworkCore.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxLink.Messaging.EntityFrameworkCore.Tests.Inbox.Registries;

public class InboxEntityFrameworkConfiguratorTests
{
    [Fact]
    public void ValidateItSelf_throws_DbContextNotConfigured_when_never_configured()
    {
        var configurator = new InboxEntityFrameworkConfigurator(new ServiceCollection(), registrationKey: null);

        Should.Throw<MessagingEntityFrameworkCoreException.DbContextNotConfigured>(configurator.ValidateItSelf);
    }

    [Fact]
    public void ValidateItSelf_does_not_throw_when_AddDbContext_is_called_exactly_once()
    {
        var configurator = new InboxEntityFrameworkConfigurator(new ServiceCollection(), registrationKey: null);

        configurator.AddDbContext<TestInboxDbContext>();

        Should.NotThrow(configurator.ValidateItSelf);
    }

    [Fact]
    public void ValidateItSelf_does_not_throw_when_DbContextFactory_is_called_exactly_once()
    {
        var configurator = new InboxEntityFrameworkConfigurator(new ServiceCollection(), registrationKey: null);

        configurator.DbContextFactory(_ => (TestInboxDbContext)null!);

        Should.NotThrow(configurator.ValidateItSelf);
    }

    [Fact]
    public void ValidateItSelf_throws_DbContextAlreadyConfigured_when_configured_more_than_once()
    {
        var configurator = new InboxEntityFrameworkConfigurator(new ServiceCollection(), registrationKey: null);

        configurator.AddDbContext<TestInboxDbContext>();
        configurator.DbContextFactory(() => (TestInboxDbContext)null!);

        Should.Throw<MessagingEntityFrameworkCoreException.DbContextAlreadyConfigured>(configurator.ValidateItSelf);
    }
}
