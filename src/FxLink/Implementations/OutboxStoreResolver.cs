using FxLink.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal sealed class OutboxStoreResolver<TMessage>(IServiceProvider serviceProvider)
    : IOutboxStoreResolver<TMessage> where TMessage : class
{
    public IOutboxStore GetOutboxStore()
    {
        var outboxStore = serviceProvider.GetKeyedService<IOutboxStore>(typeof(TMessage));
        return outboxStore ?? serviceProvider.GetService<IOutboxStore>();
    }

    public IPartitionLeaseStore GetPartitionLeaseStore()
    {
        var leaseStore = serviceProvider.GetKeyedService<IPartitionLeaseStore>(typeof(TMessage));
        return leaseStore ?? serviceProvider.GetService<IPartitionLeaseStore>();
    }
}
