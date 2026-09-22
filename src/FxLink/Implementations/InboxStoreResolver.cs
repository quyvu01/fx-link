using FxLink.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal sealed class InboxStoreResolver<TMessage>(IServiceProvider serviceProvider)
    : IInboxStoreResolver<TMessage> where TMessage : class
{
    public IInboxStore GetInboxStore()
    {
        // Check if TMessage is a batch of messages.
        if (typeof(TMessage).IsGenericType && typeof(TMessage).GetGenericTypeDefinition() == typeof(IBatch<>))
        {
            var wireMessageType = typeof(TMessage).GetGenericArguments().First();
            return serviceProvider.GetKeyedService<IInboxStore>(wireMessageType) ??
                   serviceProvider.GetService<IInboxStore>();
        }

        return serviceProvider.GetKeyedService<IInboxStore>(typeof(TMessage)) ??
               serviceProvider.GetService<IInboxStore>();
    }
}