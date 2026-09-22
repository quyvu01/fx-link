using FxLink.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal sealed class InboxStoreResolver<TMessage>(IServiceProvider serviceProvider)
    : IInboxStoreResolver<TMessage> where TMessage : class
{
    public IInboxStore GetInboxStore()
    {
        var inboxStore = serviceProvider.GetKeyedService<IInboxStore>(typeof(TMessage));
        return inboxStore ?? serviceProvider.GetService<IInboxStore>();
    }
}
