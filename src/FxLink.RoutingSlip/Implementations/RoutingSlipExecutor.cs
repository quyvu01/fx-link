using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Contexts;
using FxLink.RoutingSlip.Entities;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.RoutingSlip.Implementations;

internal sealed class RoutingSlipExecutor(IServiceProvider serviceProvider) : IRoutingSlipExecutor
{
    public async Task RunAsync([NotNull] Action<IRoutingSlipBuilder> builder, CancellationToken token = default) =>
        await RunAsync(builder, new RoutingSlipContext(Id.New(), new HeaderBag()), token);

    public async Task RunAsync(Action<IRoutingSlipBuilder> builder, IContext context, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var routingSlipBuilder = new RoutingSlipBuilder();
        builder.Invoke(routingSlipBuilder);
        var args = routingSlipBuilder.ItinerarySteps;
        var headers = routingSlipBuilder.Headers;
        if (args is not { Count: > 0 }) return;
        var nextItineraryStep = args.First();
        ItineraryStep[] remainingArgs = [.. args.Skip(1)];

        var routingSlipContext = new RoutingSlipContext(context);
        var nextArgType = Type.GetType(nextItineraryStep.AssemblyQualifiedName);
        if (nextArgType is null) return;
        var publisher = serviceProvider
            .GetRequiredService(typeof(IRoutingSlipPublisher<>).MakeGenericType(nextArgType));
        if (publisher is not RoutingSlipPublisher routingSlipPublisher) return;
        if (routingSlipPublisher is not IInternalContext internalContext) return;
        internalContext.SetContext(routingSlipContext);
        var nextArg = JsonSerializer.Deserialize(nextItineraryStep.Json, nextArgType,
            DistributedConfigurators.JsonSerializerOptions);

        await routingSlipPublisher.PublishAsync(nextArg, ctx =>
        {
            if (ctx is not RoutingSlipContext rsCtx) return;
            rsCtx.ActivityLogs = [];
            rsCtx.RemainingItineraries = remainingArgs;
            rsCtx.Variables = headers;
        }, token);
    }
}