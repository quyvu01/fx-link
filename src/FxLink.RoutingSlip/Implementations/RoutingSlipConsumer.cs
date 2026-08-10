using FxLink.Abstractions;
using FxLink.Contexts;

namespace FxLink.RoutingSlip.Implementations;

internal class RoutingSlipConsumer<TVariable> : IConsumer<TVariable> where TVariable : class
{
    public async Task ConsumeAsync(IConsumerContext<TVariable> context, CancellationToken token = default)
    {
        await Task.Yield();
    }
}