using FxLink.Abstractions;
using FxLink.Messaging.EntityFrameworkCore.Inbox.Registries;
using FxLink.Messaging.EntityFrameworkCore.Inbox.Repositories;
using FxLink.Messaging.EntityFrameworkCore.Wrappers;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Messaging.EntityFrameworkCore.Extensions;

public static class InboxDependencyExtensions
{
    public static void EntityFrameworkInbox(this IInboxConfigurator configurator,
        Action<IInboxEntityFrameworkConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var services = configurator.Services;
        var efConfigurator = new InboxEntityFrameworkConfigurator(services, registrationKey: null);
        options.Invoke(efConfigurator);
        efConfigurator.ValidateItSelf();

        services.AddScoped<IInboxStore>(sp => new EfInboxStore(sp.GetRequiredService<DbContextWrapped>().DbContext));
        configurator.Registry.RegisterDefault();
    }

    public static void EntityFrameworkInbox(this IMessageInboxConfigurator configurator,
        Action<IInboxEntityFrameworkConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var services = configurator.Services;
        var messageType = configurator.MessageType;
        var efConfigurator = new InboxEntityFrameworkConfigurator(services, messageType);
        options.Invoke(efConfigurator);
        efConfigurator.ValidateItSelf();

        services.AddKeyedScoped<IInboxStore>(messageType,
            (sp, key) => new EfInboxStore(sp.GetRequiredKeyedService<DbContextWrapped>(key).DbContext));
        configurator.Registry.RegisterKeyed(messageType);
    }
}
