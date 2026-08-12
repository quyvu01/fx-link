using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Extensions;
using FxLink.RoutingSlip.Abstractions;

namespace FxLink.RoutingSlip.Implementations;

internal abstract class RoutingSlipPublisher
{
    internal abstract Task PublishAsync(object argument, Action<IPublisherContext> options,
        CancellationToken token = default);
}

internal sealed class RoutingSlipPublisher<TArgument>(IPublisher publisher)
    : RoutingSlipPublisher, IRoutingSlipPublisher<TArgument>, IInternalContext
    where TArgument : class
{
    internal override async Task PublishAsync(object argument, Action<IPublisherContext> options,
        CancellationToken token = default) => await PublishAsync((TArgument)argument, options, token);

    public async Task PublishAsync(TArgument argument, Action<IPublisherContext> options,
        CancellationToken token = default)
    {
        if (Context is { } context) publisher.SetContext(context);
        await publisher.PublishAsync(argument, options, token);
    }

    public IContext Context { get; private set; }

    public void SetContext(IContext context) => Context = context;
}