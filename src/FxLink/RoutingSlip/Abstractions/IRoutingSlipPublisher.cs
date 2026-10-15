using FxLink.Contexts;

namespace FxLink.RoutingSlip.Abstractions;

internal interface IRoutingSlipPublisher<in TArgument> where TArgument : class
{
    Task PublishAsync(TArgument argument, Action<IPublishContext> options, CancellationToken token = default);
}