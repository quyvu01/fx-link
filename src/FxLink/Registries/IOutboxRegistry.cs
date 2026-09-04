namespace FxLink.Registries;

// Tracks which IOutboxStore/IPartitionLeaseStore pairs were registered via UseOutbox, so
// OutboxDispatcherWorker/OutboxCleanupWorker know what to resolve and act on — DI's IEnumerable<T>
// resolution doesn't surface keyed registrations, so there's no other way to discover them at startup.
// Public so external backend packages (e.g. FxLink.Outbox.EntityFrameworkCore) can register through
// it via their own IOutboxConfigurator/IMessageOutboxConfigurator extension methods.
public interface IOutboxRegistry
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
