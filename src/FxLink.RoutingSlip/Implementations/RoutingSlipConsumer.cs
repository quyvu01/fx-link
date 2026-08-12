using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.RoutingSlip.Configurators;
using FxLink.RoutingSlip.Entities;
using FxLink.RoutingSlip.Extensions;
using Microsoft.Extensions.Logging;

namespace FxLink.RoutingSlip.Implementations;

internal class RoutingSlipConsumer<TVariable>(ILogger<RoutingSlipConsumer<TVariable>> logger)
    : IConsumer<TVariable> where TVariable : class
{
    public async Task ConsumeAsync(IConsumerContext<TVariable> context, CancellationToken token = default)
    {
        await Task.Yield();

        var remainingItinerary = context.Headers
            .Get<IReadOnlyList<ItineraryStep>>(RoutingSlipHeaders.RemainingItineraryKey);
        var activityLog = context.Headers
            .Get<IReadOnlyList<ActivityLogEntry>>(RoutingSlipHeaders.ActivityLogKey);
        var variables = context.Headers.GetVariables();

        logger.LogInformation(
            "[RoutingSlipConsumer<{Variable}>] message: {@Message}, remainingItinerary: {@RemainingItinerary}, " +
            "activityLog: {@ActivityLog}, variables: {@Variables}",
            typeof(TVariable).Name, context.Message, remainingItinerary, activityLog, variables);
        // Continue with activity...
    }
}
