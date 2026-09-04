using FxLink.Abstractions;
using FxLink.Outbox.EntityFrameworkCore.Registries;
using FxLink.Outbox.EntityFrameworkCore.Repositories;
using FxLink.Outbox.EntityFrameworkCore.Wrappers;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Outbox.EntityFrameworkCore.Extensions;

public static class DependencyExtensions
{
    public static void EntityFrameworkOutbox(this IOutboxConfigurator configurator,
        Action<IOutboxEntityFrameworkConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var services = configurator.Services;
        var efConfigurator = new OutboxEntityFrameworkConfigurator(services, registrationKey: null);
        options.Invoke(efConfigurator);
        efConfigurator.ValidateItSelf();

        services.AddScoped<IOutboxStore>(sp => new EfOutboxStore(sp.GetRequiredService<DbContextWrapped>().DbContext));
        services.AddScoped<IPartitionLeaseStore>(sp =>
            new EfPartitionLeaseStore(sp.GetRequiredService<DbContextWrapped>().DbContext));
        configurator.Registry.RegisterDefault();
    }

    public static void EntityFrameworkOutbox(this IMessageOutboxConfigurator configurator,
        Action<IOutboxEntityFrameworkConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var services = configurator.Services;
        var messageType = configurator.MessageType;
        var efConfigurator = new OutboxEntityFrameworkConfigurator(services, messageType);
        options.Invoke(efConfigurator);
        efConfigurator.ValidateItSelf();

        services.AddKeyedScoped<IOutboxStore>(messageType,
            (sp, key) => new EfOutboxStore(sp.GetRequiredKeyedService<DbContextWrapped>(key).DbContext));
        services.AddKeyedScoped<IPartitionLeaseStore>(messageType,
            (sp, key) => new EfPartitionLeaseStore(sp.GetRequiredKeyedService<DbContextWrapped>(key).DbContext));
        configurator.Registry.RegisterKeyed(messageType);
    }
}
