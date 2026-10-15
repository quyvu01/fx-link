using FxLink.Abstractions;
using FxLink.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Registries;

internal sealed class MessageInboxConfigurator<TMessage>(IServiceCollection services, IInboxRegistry registry)
    : IMessageInboxConfigurator where TMessage : class
{
    public IServiceCollection Services => services;
    public IInboxRegistry Registry => registry;
    public Type MessageType => typeof(TMessage);

    public void InMemoryInbox()
    {
        services.TryAddKeyedSingleton<IInboxStore>(typeof(TMessage), (_, _) => new InMemoryInboxStore());
        registry.RegisterKeyed(typeof(TMessage));
    }
}
