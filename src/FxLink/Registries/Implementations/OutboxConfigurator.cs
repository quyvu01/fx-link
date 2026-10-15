using FxLink.Abstractions;
using FxLink.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

internal sealed class OutboxConfigurator(IServiceCollection services, IOutboxRegistry registry) : IOutboxConfigurator
{
    public IServiceCollection Services => services;
    public IOutboxRegistry Registry => registry;

    public void InMemoryOutbox()
    {
        services.AddSingleton<InMemoryPartitionLeaseStore>();
        services.AddSingleton<IPartitionLeaseStore>(sp => sp.GetRequiredService<InMemoryPartitionLeaseStore>());
        services.AddSingleton<IOutboxStore, InMemoryOutboxStore>();
        registry.RegisterDefault();
    }

    public void DispatcherOptions(Action<IOutboxDispatcherOptions> options = null)
    {
        var outboxDispatcherOptions = new OutboxDispatcherOptions();
        options?.Invoke(outboxDispatcherOptions);
        services.AddSingleton<IOutboxDispatcherOptions>(_ => outboxDispatcherOptions);
    }

    public void MessageOutbox<TMessage>(Action<IMessageOutboxConfigurator> options) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(options);
        var messageOutboxConfigurator = new MessageOutboxConfigurator<TMessage>(services, registry);
        options.Invoke(messageOutboxConfigurator);
    }
}
