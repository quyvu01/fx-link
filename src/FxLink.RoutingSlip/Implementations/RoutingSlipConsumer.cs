using System.Collections.Concurrent;
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Configurators;
using FxLink.RoutingSlip.Entities;
using FxLink.RoutingSlip.Extensions;
using FxLink.Wrappers;
using Microsoft.Extensions.Logging;

namespace FxLink.RoutingSlip.Implementations;

internal class RoutingSlipConsumer<TVariable>(IServiceProvider services, ILogger<RoutingSlipConsumer<TVariable>> logger)
    : IConsumer<TVariable> where TVariable : class
{
    private static readonly ConcurrentDictionary<(Type ArgumentType, Type ConsumerType), Type> ActivityLookup = [];

    public async Task ConsumeAsync(IConsumerContext<TVariable> context, CancellationToken token = default)
    {
        var remainingItinerary = context.Headers
            .Get<IReadOnlyList<ItineraryStep>>(RoutingSlipHeaders.RemainingItineraryKey);
        var activityLog = context.Headers
            .Get<IReadOnlyList<ActivityLogEntry>>(RoutingSlipHeaders.ActivityLogKey);
        var variables = context.Headers.GetVariables();

        // Continue with activity...
        var consumerContextWrapped = context.GetPayload<ConsumerContextWrapped>();
        var consumerType = consumerContextWrapped.ConsumerType;
        if (!typeof(IExecuteActivity).IsAssignableFrom(consumerType)) return;
        if (context.Message is ActivityLogEntry logEntry)
        {
            // Do with log entry, compensated...
            return;
        }

        var argumentType = typeof(TVariable);

        var activityType = ActivityLookup.GetOrAdd((argumentType, consumerType),
            static types => types.ConsumerType
                .GetInterfaces()
                .Where(a => a.IsGenericType)
                .Where(a =>
                {
                    var genericTypeDefinition = a.GetGenericTypeDefinition();
                    return genericTypeDefinition == typeof(IExecuteActivity<>) ||
                           genericTypeDefinition == typeof(IExecuteActivity<,>);
                })
                .FirstOrDefault(a => a.GetGenericArguments().First() == types.ArgumentType));
        if (activityType is null) return;
        var activity = services.GetService(activityType);
        switch (activity)
        {
            case ExecuteActivityArgProxy executeActivityArgProxy:
            {
                var result = await executeActivityArgProxy.ExecuteAsync(context.Message, context, token);
                if (result.IsCompleted)
                {
                    // Do next activity
                    // Then return
                }
                
                break;
            }
            case ExecuteActivityArgLogProxy executeActivityArgLogProxy:
                break;
        }
    }
}