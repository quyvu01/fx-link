using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IMessageOutboxConfigurator
{
    // Same reasoning as IOutboxConfigurator.Services/Registry — lets an external backend package
    // register a keyed IOutboxStore/IPartitionLeaseStore for MessageType without needing a generic
    // TMessage parameter on this interface (DI keyed services only need the runtime Type as the key).
    IServiceCollection Services { get; }
    IOutboxRegistry Registry { get; }
    Type MessageType { get; }

    void InMemoryOutbox();
}
