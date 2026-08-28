using FxLink.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

// Tracks which IOutboxStore/IPartitionLeaseStore pairs were registered via UseOutbox, so
// OutboxDispatcherWorker/OutboxCleanupWorker know what to resolve and act on — DI's IEnumerable<T>
// resolution doesn't surface keyed registrations, so there's no other way to discover them at startup.
internal interface IOutboxRegistry
{
    bool HasDefault { get; }
    IReadOnlyCollection<Type> KeyedMessageTypes { get; }
    void RegisterDefault();
    void RegisterKeyed(Type messageType);
}

internal sealed class OutboxRegistry : IOutboxRegistry
{
    private readonly HashSet<Type> _keyedMessageTypes = [];

    public bool HasDefault { get; private set; }
    public IReadOnlyCollection<Type> KeyedMessageTypes => _keyedMessageTypes;

    public void RegisterDefault() => HasDefault = true;
    public void RegisterKeyed(Type messageType) => _keyedMessageTypes.Add(messageType);
}

internal static class OutboxRegistryExtensions
{
    public static IEnumerable<IOutboxStore> ResolveOutboxStores(this IOutboxRegistry registry,
        IServiceProvider serviceProvider)
    {
        if (registry.HasDefault) yield return serviceProvider.GetRequiredService<IOutboxStore>();

        foreach (var messageType in registry.KeyedMessageTypes)
            yield return serviceProvider.GetRequiredKeyedService<IOutboxStore>(messageType);
    }

    public static IEnumerable<(IOutboxStore Outbox, IPartitionLeaseStore Leases)> ResolveOutboxStoresWithLeases(
        this IOutboxRegistry registry, IServiceProvider serviceProvider)
    {
        if (registry.HasDefault)
            yield return (serviceProvider.GetRequiredService<IOutboxStore>(),
                serviceProvider.GetRequiredService<IPartitionLeaseStore>());

        foreach (var messageType in registry.KeyedMessageTypes)
            yield return (serviceProvider.GetRequiredKeyedService<IOutboxStore>(messageType),
                serviceProvider.GetRequiredKeyedService<IPartitionLeaseStore>(messageType));
    }
}
