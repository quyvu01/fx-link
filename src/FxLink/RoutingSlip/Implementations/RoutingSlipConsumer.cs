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
using FxLink.RoutingSlip.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.RoutingSlip.Implementations;

internal class RoutingSlipConsumer<TVariable>(
    IServiceProvider services,
    IDynamicActivityRegistry dynamicActivityRegistry,
    ILogger<RoutingSlipConsumer<TVariable>> logger)
    : IConsumer<TVariable> where TVariable : class
{
    private static readonly ConcurrentDictionary<(Type ArgumentType, Type ConsumerType), Type> ActivityLookup = [];

    private static readonly ConcurrentDictionary<Type, (Type ArgumentType, Type LogType)?> CompensateShapeLookup = [];

    // Same shape as CompensateShapeLookup, generalized to also cover the no-logs case (LogType can
    // itself be null even when a match is found). Only the DynamicRoutingMessage branch needs
    // this: there, argumentType isn't known upfront the way it is for a normal typed delivery
    // (there, the wire's own exchange already implies TVariable) — it has to be discovered from
    // consumerType's own IExecuteActivity<>/IExecuteActivity<,> shape, same as compensate does.
    private static readonly ConcurrentDictionary<Type, (Type ArgumentType, Type LogType)?> DynamicShapeLookup = [];

    public async Task ConsumeAsync(IConsumeContext<TVariable> context, CancellationToken token = default)
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

        if (context.Message is DynamicRoutingMessage dynamicMessage)
        {
            await ConsumeDynamicAsync(dynamicMessage, consumerType, remainingItinerary, activityLog, variables,
                context, token);
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
        var activityProxy = services.GetService(activityProxyType);
        await DispatchExecuteAsync(activityProxy, context.Message, argumentType, remainingItinerary, activityLog,
            variables, context, token);
    }

    // Entry point for a Uri-addressed step. consumerType is already correctly resolved (each
    // dynamically-addressed activity gets its own queue on DynamicRoutingMessage's shared exchange,
    // per RoutingSlipConfigurator), so this only needs to (a) confirm the message is actually meant
    // for THIS activityType — several activities share the fanout, same as ActivityLogEntry — and
    // (b) recover the real argument type/shape that AddActivity<TActivity>(uri) discovered via
    // reflection at startup, since the sender never had (or wanted) a compile-time reference to it.
    private async Task ConsumeDynamicAsync(DynamicRoutingMessage message, Type consumerType,
        IReadOnlyList<ItineraryStep> remainingItineraries, IReadOnlyList<ActivityLogEntry> activityLogs,
        IHeaders variables, IContext context, CancellationToken token)
    {
        var myDestination = dynamicActivityRegistry.GetDestination(consumerType);
        if (myDestination is null || myDestination != message.Destination)
            return; // this call belongs to a different activity sharing the fanout exchange

        var shape = DynamicShapeLookup.GetOrAdd(consumerType, static type =>
        {
            var executeActivityInterface = type.GetInterfaces()
                .Where(a => a.IsGenericType)
                .FirstOrDefault(a => a.GetGenericTypeDefinition() == typeof(IExecuteActivity<>) ||
                                     a.GetGenericTypeDefinition() == typeof(IExecuteActivity<,>));
            if (executeActivityInterface is null) return null;
            var genericArguments = executeActivityInterface.GetGenericArguments();
            return (genericArguments[0], genericArguments.Length == 2 ? genericArguments[1] : null);
        });
        if (shape is not { } resolvedShape) return;

        var (argumentType, logType) = resolvedShape;
        var argument = JsonSerializer.Deserialize(message.Json, argumentType,
            DistributedConfigurators.JsonSerializerOptions);

        var activityProxyType = logType is null
            ? typeof(IExecuteActivityProxy<>).MakeGenericType(argumentType)
            : typeof(IExecuteActivityProxy<,>).MakeGenericType(argumentType, logType);
        var activityProxy = services.GetService(activityProxyType);

        await DispatchExecuteAsync(activityProxy, argument, argumentType, remainingItineraries, activityLogs,
            variables, context, token);
    }

    // Shared by the normal typed path and the dynamic (Uri) path once each has resolved: the real
    // argument object, its real type, and the matching activity proxy instance. Everything past
    // that point — call Execute, branch on Completed/Fault, advance or compensate — is identical
    // regardless of how the caller found its way here.
    private async Task DispatchExecuteAsync(object activityProxy, object argument, Type argumentType,
        IReadOnlyList<ItineraryStep> remainingItineraries, IReadOnlyList<ActivityLogEntry> activityLogs,
        IHeaders variables, IContext context, CancellationToken token)
    {
        switch (activityProxy)
        {
            case ExecuteActivityArgProxy executeActivityArgProxy:
            {
                var result = await executeActivityArgProxy.ExecuteAsync(argument, context, token);
                if (result.IsCompleted)
                {
                    await PublishNextAsync(remainingItineraries, activityLogs, variables, context, token);
                    return;
                }

                logger.LogWarning(result.Exception,
                    "[RoutingSlipConsumer] Execute faulted for {ArgumentType} on routing slip " +
                    "{CorrelationId}; starting compensate.", argumentType.Name, context.CorrelationId);
                await PublishCompensateAsync(activityLogs, variables, context, token);
                return;
            }
            case ExecuteActivityArgLogProxy executeActivityArgLogProxy:
            {
                var result = await executeActivityArgLogProxy.ExecuteAsync(argument, context, token);
                if (result.IsCompleted)
                {
                    var completedEntry = new ActivityLogEntry(
                        argumentType.AssemblyQualifiedName,
                        result.Log?.GetType().AssemblyQualifiedName,
                        JsonSerializer.Serialize(result.Log, result.Log?.GetType() ?? typeof(object),
                            DistributedConfigurators.JsonSerializerOptions));
                    ActivityLogEntry[] updatedActivityLog = [.. activityLogs ?? [], completedEntry];

                    await PublishNextAsync(remainingItineraries, updatedActivityLog, variables, context, token);
                    return;
                }

                logger.LogWarning(result.Exception,
                    "[RoutingSlipConsumer] Execute faulted for {ArgumentType} on routing slip " +
                    "{CorrelationId}; starting compensate.", argumentType.Name, context.CorrelationId);
                await PublishCompensateAsync(activityLogs, variables, context, token);
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