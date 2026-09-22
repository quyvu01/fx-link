namespace FxLink.Registries;

// Tracks which IInboxStore registrations were made via UseInbox, mirroring IOutboxRegistry — a
// future InboxCleanupWorker needs this the same way OutboxCleanupWorker needs IOutboxRegistry, since
// DI's IEnumerable<T> resolution doesn't surface keyed registrations, so there's no other way to
// discover them at startup. Public so external backend packages (e.g. a future
// FxLink.Inbox.EntityFrameworkCore) can register through it via their own
// IInboxConfigurator/IMessageInboxConfigurator extension methods.
public interface IInboxRegistry
{
    bool HasDefault { get; }
    IReadOnlyCollection<Type> KeyedMessageTypes { get; }
    void RegisterDefault();
    void RegisterKeyed(Type messageType);
}

internal sealed class InboxRegistry : IInboxRegistry
{
    private readonly HashSet<Type> _keyedMessageTypes = [];

    public bool HasDefault { get; private set; }
    public IReadOnlyCollection<Type> KeyedMessageTypes => _keyedMessageTypes;

    public void RegisterDefault() => HasDefault = true;
    public void RegisterKeyed(Type messageType) => _keyedMessageTypes.Add(messageType);
}
