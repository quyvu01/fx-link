using FxLink.Abstractions;
using FxLink.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Registries;

internal sealed class MessageOutboxConfigurator<TMessage>(IServiceCollection services, IOutboxRegistry registry)
    : IMessageOutboxConfigurator where TMessage : class
{
    public void InMemoryOutbox()
    {
        // Same pairing as OutboxConfigurator.InMemoryOutbox(), but keyed by TMessage — explicit
        // factories because DI won't auto-resolve a keyed constructor dependency without
        // [FromKeyedServices], which InMemoryOutboxStore can't use since it's shared with the
        // unkeyed registration path too.
        services.TryAddKeyedSingleton<InMemoryPartitionLeaseStore>(typeof(TMessage));
        services.TryAddKeyedSingleton<IPartitionLeaseStore>(typeof(TMessage),
            (sp, key) => sp.GetRequiredKeyedService<InMemoryPartitionLeaseStore>(key));
        services.TryAddKeyedSingleton<IOutboxStore>(typeof(TMessage),
            (sp, key) => new InMemoryOutboxStore(sp.GetRequiredKeyedService<InMemoryPartitionLeaseStore>(key)));
        registry.RegisterKeyed(typeof(TMessage));
    }
}