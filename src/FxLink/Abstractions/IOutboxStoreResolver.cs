namespace FxLink.Abstractions;

// Resolves which IOutboxStore/IPartitionLeaseStore applies to TMessage — keyed-by-message-type
// registration (IMessageOutboxConfigurator) wins if present, otherwise falls back to the default
// registered via IOutboxConfigurator. This is a write-path lookup, not the background dispatch loop.
public interface IOutboxStoreResolver<TMessage> where TMessage : class
{
    IOutboxStore GetOutboxStore();
    IPartitionLeaseStore GetPartitionLeaseStore();
}
