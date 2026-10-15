using FxLink.Abstractions;
using FxLink.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

internal sealed class InboxConfigurator(IServiceCollection services, IInboxRegistry registry) : IInboxConfigurator
{
    public IServiceCollection Services => services;
    public IInboxRegistry Registry => registry;

    public void InMemoryInbox()
    {
        services.AddSingleton<IInboxStore, InMemoryInboxStore>();
        registry.RegisterDefault();
    }

    public void Options(Action<IInboxOptions> options = null)
    {
        var inboxOptions = new InboxOptions();
        options?.Invoke(inboxOptions);
        inboxOptions.Validate();
        services.AddSingleton<IInboxOptions>(_ => inboxOptions);
    }

    public void MessageInbox<TMessage>(Action<IMessageInboxConfigurator> options) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(options);
        var messageInboxConfigurator = new MessageInboxConfigurator<TMessage>(services, registry);
        options.Invoke(messageInboxConfigurator);
    }
}
