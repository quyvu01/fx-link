using System.Diagnostics.CodeAnalysis;
using FxLink.Contexts;

namespace FxLink.RoutingSlip.Abstractions;

public interface IRoutingSlipExecutor
{
    Task RunAsync([NotNull] Action<IRoutingSlipBuilder> builder, CancellationToken token = default);
    Task RunAsync([NotNull] Action<IRoutingSlipBuilder> builder, IContext context, CancellationToken token = default);
}