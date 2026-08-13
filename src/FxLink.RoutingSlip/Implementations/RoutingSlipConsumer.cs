using System.Collections.Concurrent;
using System.Text.Json;
using FxLink.Abstractions;
using FxLink.Configurators;
using FxLink.Contexts;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Configurators;
using FxLink.RoutingSlip.Contexts;
using FxLink.RoutingSlip.Entities;
using FxLink.RoutingSlip.Extensions;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.RoutingSlip.Implementations;

internal class RoutingSlipConsumer<TVariable>(IServiceProvider services, ILogger<RoutingSlipConsumer<TVariable>> logger)
    : IConsumer<TVariable> where TVariable : class
{
    private static readonly ConcurrentDictionary<(Type ArgumentType, Type ConsumerType), Type> ActivityLookup = [];
    
    private static readonly ConcurrentDictionary<Type, (Type ArgumentType, Type LogType)?> CompensateShapeLookup = [];

    public async Task ConsumeAsync(IConsumerContext<TVariable> context, CancellationToken token = default)
    {
        var remainingItinerary = context.Headers
            .Get<IReadOnlyList<ItineraryStep>>(RoutingSlipHeaders.RemainingItineraryKey);
        var activityLog = context.Headers
            .Get<IReadOnlyList<ActivityLogEntry>>(RoutingSlipHeaders.ActivityLogKey);
        var variables = context.Headers.GetVariables();

        var consumerContextWrapped = context.GetPayload<ConsumerContextWrapped>();
        var consumerType = consumerContextWrapped.ConsumerType;
        if (!typeof(IExecuteActivity).IsAssignableFrom(consumerType)) return;
        if (context.Message is ActivityLogEntry logEntry)
        {
            await CompensateAsync(logEntry, consumerType, activityLog, variables, context, token);
            return;
        }

        var argumentType = typeof(TVariable);

        var activityProxyType = ActivityLookup.GetOrAdd((argumentType, consumerType),
            static types =>
            {
                var executeActivityInterface = types.ConsumerType
                    .GetInterfaces()
                    .Where(a => a.IsGenericType)
                    .Where(a =>
                    {
                        var genericTypeDefinition = a.GetGenericTypeDefinition();
                        return genericTypeDefinition == typeof(IExecuteActivity<>) ||
                               genericTypeDefinition == typeof(IExecuteActivity<,>);
                    })
                    .FirstOrDefault(a => a.GetGenericArguments().First() == types.ArgumentType);
                if (executeActivityInterface is null) return null;

                var genericArguments = executeActivityInterface.GetGenericArguments();
                var proxyOpenType = genericArguments.Length == 1
                    ? typeof(IExecuteActivityProxy<>)
                    : typeof(IExecuteActivityProxy<,>);
                return proxyOpenType.MakeGenericType(genericArguments);
            });
        if (activityProxyType is null) return;
        var activity = services.GetService(activityProxyType);
        switch (activity)
        {
            case ExecuteActivityArgProxy executeActivityArgProxy:
            {
                var result = await executeActivityArgProxy.ExecuteAsync(context.Message, context, token);
                if (result.IsCompleted)
                {
                    await PublishNextAsync(remainingItinerary, activityLog, variables, context, token);
                    return;
                }

                logger.LogWarning(result.Exception,
                    "[RoutingSlipConsumer] Execute faulted for {ArgumentType} on routing slip " +
                    "{CorrelationId}; starting compensate.", argumentType.Name, context.CorrelationId);
                await PublishCompensateAsync(activityLog, variables, context, token);
                return;
            }
            case ExecuteActivityArgLogProxy executeActivityArgLogProxy:
            {
                var result = await executeActivityArgLogProxy.ExecuteAsync(context.Message, context, token);
                if (result.IsCompleted)
                {
                    var completedEntry = new ActivityLogEntry(
                        argumentType.AssemblyQualifiedName,
                        result.Log?.GetType().AssemblyQualifiedName,
                        JsonSerializer.Serialize(result.Log, result.Log?.GetType() ?? typeof(object),
                            DistributedConfigurators.JsonSerializerOptions));
                    ActivityLogEntry[] updatedActivityLog = [.. activityLog ?? [], completedEntry];

                    await PublishNextAsync(remainingItinerary, updatedActivityLog, variables, context, token);
                    return;
                }

                logger.LogWarning(result.Exception,
                    "[RoutingSlipConsumer] Execute faulted for {ArgumentType} on routing slip " +
                    "{CorrelationId}; starting compensate.", argumentType.Name, context.CorrelationId);
                await PublishCompensateAsync(activityLog, variables, context, token);
                return;
            }
        }
    }

    private async Task PublishNextAsync(IReadOnlyList<ItineraryStep> remainingItineraries,
        IReadOnlyList<ActivityLogEntry> activityLogEntries, IHeaders variables, IContext context,
        CancellationToken token = default)
    {
        if (remainingItineraries is not { Count: > 0 })
        {
            logger.LogInformation("[RoutingSlipConsumer] Routing slip {CorrelationId} completed.",
                context.CorrelationId);
            return;
        }

        var nextItineraryStep = remainingItineraries[0];
        ItineraryStep[] remainingArgs = [.. remainingItineraries.Skip(1)];
        var nextArgType = Type.GetType(nextItineraryStep.AssemblyQualifiedName);
        if (nextArgType is null) return;

        var routingSlipContext = new RoutingSlipContext(context);
        var publisher = services.GetRequiredService(typeof(IRoutingSlipPublisher<>).MakeGenericType(nextArgType));
        if (publisher is not RoutingSlipPublisher routingSlipPublisher) return;
        if (routingSlipPublisher is not IInternalContext internalContext) return;
        internalContext.SetContext(routingSlipContext);

        var nextArg = JsonSerializer.Deserialize(nextItineraryStep.Json, nextArgType,
            DistributedConfigurators.JsonSerializerOptions);

        await routingSlipPublisher.PublishAsync(nextArg, ctx =>
        {
            if (ctx is not RoutingSlipContext rsCtx) return;
            rsCtx.ActivityLogs = activityLogEntries;
            rsCtx.RemainingItineraries = remainingArgs;
            rsCtx.Variables = variables;
        }, token);
    }

    private async Task CompensateAsync(ActivityLogEntry logEntry, Type consumerType,
        IReadOnlyList<ActivityLogEntry> remainingActivityLog, IHeaders variables, IContext context,
        CancellationToken token)
    {
        var compensateShape = CompensateShapeLookup.GetOrAdd(consumerType, static type =>
        {
            var executeActivityInterface = type.GetInterfaces()
                .FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IExecuteActivity<,>));
            if (executeActivityInterface is null) return null;
            var genericArguments = executeActivityInterface.GetGenericArguments();
            return (genericArguments[0], genericArguments[1]);
        });
        if (compensateShape is not { } shape) return;

        var (argumentType, logType) = shape;
        if (argumentType.AssemblyQualifiedName != logEntry.ArgumentAssemblyQualifiedName ||
            logType.AssemblyQualifiedName != logEntry.LogAssemblyQualifiedName)
            return; // this entry belongs to a different activity sharing the fanout exchange

        var proxyType = typeof(IExecuteActivityProxy<,>).MakeGenericType(argumentType, logType);
        if (services.GetService(proxyType) is not ExecuteActivityArgLogProxy proxy) return;

        var log = JsonSerializer.Deserialize(logEntry.LogJson, logType, DistributedConfigurators.JsonSerializerOptions);
        var compensateResult = await proxy.CompensateAsync(log, context, token);
        if (!compensateResult.IsCompensated)
            logger.LogWarning(compensateResult.Exception,
                "[RoutingSlipConsumer] Compensate faulted for {LogType} on routing slip {CorrelationId}; " +
                "continuing the rollback.", logType.Name, context.CorrelationId);

        await PublishCompensateAsync(remainingActivityLog, variables, context, token);
    }

    private async Task PublishCompensateAsync(IReadOnlyList<ActivityLogEntry> activityLogEntries,
        IHeaders variables, IContext context, CancellationToken token = default)
    {
        if (activityLogEntries is not { Count: > 0 })
        {
            logger.LogInformation("[RoutingSlipConsumer] Routing slip {CorrelationId} compensated.",
                context.CorrelationId);
            return;
        }

        var entryToCompensate = activityLogEntries[^1];
        ActivityLogEntry[] remainingActivityLog = [.. activityLogEntries.Take(activityLogEntries.Count - 1)];

        var routingSlipContext = new RoutingSlipContext(context);
        var publisher = services.GetRequiredService<IRoutingSlipPublisher<ActivityLogEntry>>();
        if (publisher is IInternalContext internalContext) internalContext.SetContext(routingSlipContext);

        await publisher.PublishAsync(entryToCompensate, ctx =>
        {
            if (ctx is not RoutingSlipContext rsCtx) return;
            rsCtx.ActivityLogs = remainingActivityLog;
            rsCtx.RemainingItineraries = [];
            rsCtx.Variables = variables;
        }, token);
    }
}