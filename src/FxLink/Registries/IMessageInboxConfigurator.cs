using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IMessageInboxConfigurator
{
    // Same reasoning as IInboxConfigurator.Services/Registry — lets an external backend package
    // register a keyed IInboxStore for MessageType without needing a generic TMessage parameter on
    // this interface (DI keyed services only need the runtime Type as the key).
    IServiceCollection Services { get; }
    IInboxRegistry Registry { get; }
    Type MessageType { get; }

    void InMemoryInbox();
}
