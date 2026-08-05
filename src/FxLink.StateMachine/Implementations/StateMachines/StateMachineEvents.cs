using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Extensions;
using FxLink.Faults;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Contexts;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Extensions;
using FxLink.StateMachine.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Implementations.StateMachines;

public abstract partial class StateMachine<TInstance>
{
    private const string CompletedSuffix = nameof(IRequest<,>.Completed);
    private const string FailedSuffix = nameof(IRequest<,>.Failed);
    private const string TimeoutExpiredSuffix = nameof(IRequest<,>.TimeoutExpired);
    private const string ReceivedSuffix = nameof(ISchedule<>.Received);

    private Dictionary<Type, string> _eventSuffixCache;

    // Requests publish/watchdog messages carry only the activity's root name (e.g. "PaymentRequest"),
    // never a pre-baked "PaymentRequest.Completed" — that would leak unchanged through retry/dead-letter
    // republishing onto Fault<TRequest>/RequestTimeoutExpired<TRequest> messages, which need a different
    // suffix. The consumer resolves the full name here, from the concrete TMessage of *this* delivery.
    private string ResolveEventName<TMessage>(string rootName) where TMessage : class
    {
        var messageType = typeof(TMessage);
        if (messageType.IsGenericType)
        {
            var genericDefinition = messageType.GetGenericTypeDefinition();
            if (genericDefinition == typeof(Fault<>)) return $"{rootName}.{FailedSuffix}";
            if (genericDefinition == typeof(RequestTimeoutExpired<>)) return $"{rootName}.{TimeoutExpiredSuffix}";
        }

        _eventSuffixCache ??= BuildEventSuffixCache();
        return _eventSuffixCache.TryGetValue(messageType, out var suffix) ? $"{rootName}.{suffix}" : rootName;
    }

    // Maps a bare message type to the suffix of whichever registered activity produces it: a Request's
    // TResponse resolves to "Completed", a Schedule's TMessage resolves to "Received". Built once from
    // InternalActivityConfigurators, which is fully populated by the time any event is first raised.
    private Dictionary<Type, string> BuildEventSuffixCache()
    {
        var map = new Dictionary<Type, string>();
        foreach (var activity in InternalActivityConfigurators.Keys)
        foreach (var iface in activity.GetType().GetInterfaces())
        {
            if (!iface.IsGenericType) continue;
            var genericDefinition = iface.GetGenericTypeDefinition();
            if (genericDefinition == typeof(IRequest<,>))
                map[iface.GetGenericArguments()[1]] = CompletedSuffix;
            else if (genericDefinition == typeof(ISchedule<>))
                map[iface.GetGenericArguments()[0]] = ReceivedSuffix;
        }

        return map;
    }

    // Note this raise event may have a lot of edge cases like race condition, state fall -> outbox and inbox
    public async Task RaiseEventAsync<TMessage>(IConsumerContext<TMessage> context, CancellationToken token = default)
        where TMessage : class
    {
        var @event = new Event<TMessage>();
        // Here, we will resolve the activity
        if (context.Headers.Get<string>(DistributedConfigurators.Headers.MessageRoutingKey) is { } rootName)
            @event.SetName(ResolveEventName<TMessage>(rootName));

        var deliveryKind = context.Headers.Get<string>(DistributedConfigurators.Headers.DeliveryKindKey);

        var isMessageDelaying = deliveryKind == DistributedConfigurators.DeliveryKinds.Delay;

        var activityConfigurator = _messageConfigurators
            .FirstOrDefault(a => a.Key.Equals(@event));

        if (activityConfigurator.Value is not EventConfigurator<TInstance, TMessage> configurator) return;

        var services = context.GetPayload<IServiceProvider>();
        var machineInstanceRepository = services.GetRequiredService<IStateMachineInstanceRepository<TInstance>>();


        var statesWithOperators = _stateMapEventOperators
            .Aggregate(new List<(IState State, IEventOperator<TInstance, TMessage> EventOperator)>(),
                (acc, next) =>
                {
                    var operators = next.Value
                        .OfType<IEventOperator<TInstance, TMessage>>()
                        .ToArray();
                    var matchedOperator = operators.FirstOrDefault(f => f.Event.Equals(@event));
                    if (matchedOperator is null) return acc;
                    acc.Add((next.Key, matchedOperator));
                    return acc;
                });

        if (statesWithOperators is not { Count: > 0 })
            throw new StateMachineException.NoEventOperatorMatchesEvent(typeof(IEvent<TMessage>));

        var predicate = configurator.GetPredicate(context);
        // If the configurator just have the CorrelationBy, then we have the get the correlationId from instance
        var correlationId = configurator.GetCorrelationId(context) ??
                            await machineInstanceRepository.GetCorrelationIdAsync(predicate, token);

        await using var scope = await machineInstanceRepository.BeginScopeAsync(correlationId, token: token);

        var instance = await machineInstanceRepository.GetInstanceAsync(predicate, token);
        if (instance is null)
        {
            if (isMessageDelaying) return; // Completed?
            if (statesWithOperators.All(x => x.State != Initial))
            {
                var missingInstanceAction = configurator.MissingInstanceBehavior;
                if (missingInstanceAction is null)
                    throw new StateMachineException.InstanceMustBeInitializedFirst();
                // Handling missing instance
                var missingInstanceConfigurator = new MissingInstanceConfigurator<TInstance, TMessage>();
                var missingInvocationResult = missingInstanceAction
                    .Invoke(missingInstanceConfigurator);
                await missingInvocationResult.SendAsync(context, token);
                return;
            }

            // It means that we already have the initial state => need to init a new instance.
            var correlationIdSelector = configurator.CorrelationSelector;
            var newCorrelationId = correlationIdSelector.Compile().Invoke(context);
            instance = await machineInstanceRepository.CreateInstanceAsync(newCorrelationId, token);
        }

        var eventOperator = statesWithOperators
            .FirstOrDefault(x => (State)x.State == new State(instance.State)).EventOperator;
        if (eventOperator is null)
            throw new StateMachineException.EventNotDeclaredForState(typeof(TMessage), instance.State);

        if (_innerMessageConfigurators.TryGetValue(@event, out var c) &&
            c is IScheduleConfigurator<TInstance, TMessage> scheduleConfigurator)
        {
            var tokenGetter = scheduleConfigurator.TokenIdProvider.GetGetter();
            var tokenId = tokenGetter.Invoke(instance);
            if (tokenId is null) return; // It means that we've unschedule the event
            // Then, if this is the schedule received event, we have to set tokenId to null as well.
            var tokenIdSetter = scheduleConfigurator.TokenIdProvider.GetSetter();
            tokenIdSetter.Invoke(instance, null);
        }

        var stateMachineContext = new StateMachineContext<TInstance, TMessage>
            (instance, context.Message, context.RequesterId, context);
        stateMachineContext.SetPayload(services);
        stateMachineContext.SetPayload(context.GetPayload<ConsumerContextWrapped>());
        await eventOperator.ExecuteAsync(stateMachineContext, token);
        if (_removeInstanceWhenCompleted && instance.State == Completed.Name)
            await machineInstanceRepository.RemoveInstanceAsync(instance, token);
        await machineInstanceRepository.SaveInstanceAsync(token);
        await scope.CommitAsync(token);
    }
}