using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Implementations;

internal sealed class RoutingSlipExecutor : IRoutingSlipExecutor
{
    public Task RunAsync(Action<IRoutingSlipBuilder> builder)
    {
        throw new NotImplementedException();
    }
}