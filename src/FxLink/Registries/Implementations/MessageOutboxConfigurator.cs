using FxLink.Abstractions;
using FxLink.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Registries;

internal sealed class MessageOutboxConfigurator<TMessage>(IServiceCollection services, IOutboxRegistry registry)
    : IMessageOutboxConfigurator where TMessage : class
{
    public IServiceCollection Services => services;
    public IOutboxRegistry Registry => registry;
    public Type MessageType => typeof(TMessage);

    public void InMemoryOutbox()
    {
        services.TryAddKeyedSingleton<InMemoryPartitionLeaseStore>(typeof(TMessage));
        services.TryAddKeyedSingleton<IPartitionLeaseStore>(typeof(TMessage),
            (sp, key) => sp.GetRequiredKeyedService<InMemoryPartitionLeaseStore>(key));
        services.TryAddKeyedSingleton<IOutboxStore>(typeof(TMessage),
            (sp, key) => new InMemoryOutboxStore(sp.GetRequiredKeyedService<InMemoryPartitionLeaseStore>(key)));
        registry.RegisterKeyed(typeof(TMessage));
    }
}
