using System.Diagnostics.CodeAnalysis;

namespace FxLink.RoutingSlip.Abstractions;

public interface IRoutingSlipExecutor
{
    Task RunAsync([NotNull] Action<IRoutingSlipBuilder> builder);
}