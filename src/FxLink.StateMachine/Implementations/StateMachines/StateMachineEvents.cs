using FxLink.Abstractions.Contexts;
using FxLink.Configurators;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Configurators;
using FxLink.StateMachine.Contexts;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Extensions;
using FxLink.StateMachine.Registries;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Implementations.StateMachines;

public abstract partial class StateMachine<TInstance>
{
    // Note this raise event may have a lot of edge cases like race condition, state fall -> outbox and inbox
    public async Task RaiseEventAsync<TMessage>(IConsumerContext<TMessage> context, CancellationToken token = default)
        where TMessage : class
    {
        var @event = new Event<TMessage>();
        // Here, we will resolve the activity
        if (context.Headers.Get<string>(StateMachineConfigurators.MessageRoutingKey) is { } messageKey)
            @event.SetName(messageKey);

        var messageType = context.Headers.Get<string>(DistributedConfigurators.Headers.MessageKindKey);

        var isMessageDelaying = messageType == DistributedConfigurators.MessageKinds.Delay;

        var activityConfigurator = _messageConfigurators
            .FirstOrDefault(a => a.Key.Equals(@event));

        if (activityConfigurator.Value is not EventConfigurator<TInstance, TMessage> configurator) return;

        var services = ConsumerAmbient.Services;
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
        await eventOperator.ExecuteAsync(stateMachineContext, token);
        if (_removeInstanceWhenCompleted && instance.State == Completed.Name)
            await machineInstanceRepository.RemoveInstanceAsync(instance, token);
        await machineInstanceRepository.SaveInstanceAsync(token);
        await scope.CommitAsync(token);
    }
}