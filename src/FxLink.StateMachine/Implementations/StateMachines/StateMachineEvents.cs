using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Contexts;
using FxLink.StateMachine.Exceptions;
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
        var services = ServiceProviderAmbient.Services;
        var @event = new Event<TMessage>();
        var eventConfig = _activityConfigurations.FirstOrDefault(a => a.Event.Equals(@event));
        if (eventConfig?.Configurator is not EventConfigurator<TInstance, TMessage> config) return;
        var predicate = config.GetPredicate(context);
        var machineInstanceRepository = services.GetRequiredService<IStateMachineInstanceRepository<TInstance>>();

        var statesWithFlows = _stateMapFlows
            .Aggregate(new List<(IState State, IFlowOperator<TInstance, TMessage> FlowOperator)>(),
                (acc, next) =>
                {
                    var flows = next.Value
                        .OfType<IFlowOperator<TInstance, TMessage>>()
                        .ToArray();
                    var matchedFlow = flows.FirstOrDefault(f => f.Event.Equals(@event));
                    if (matchedFlow is null) return acc;
                    acc.Add((next.Key, matchedFlow));
                    return acc;
                });

        if (statesWithFlows is not { Count: > 0 })
            throw new StateMachineException.EventDoesNotMatchAnyFlow(typeof(IEvent<TMessage>));

        var instance = await machineInstanceRepository.GetInstanceAsync(predicate, token);
        if (instance is null)
        {
            if (statesWithFlows.All(x => x.State != Initial))
            {
                var missingInstanceAction = config.MissingInstanceBehavior;
                if (missingInstanceAction is null)
                    throw new StateMachineException.StateMachineInstanceMustBeInitFirst();
                // Handling missing instance
                var missingInstanceConfigurator = new MissingInstanceConfigurator<TInstance, TMessage>();
                var missingInvocationResult = missingInstanceAction
                    .Invoke(missingInstanceConfigurator);
                await missingInvocationResult.SendAsync(context, token);
                return;
            }

            // It means that we already have the initial state => need to init a new instance.
            var correlationIdSelector = config.CorrelationSelector;
            var newCorrelationId = correlationIdSelector.Compile().Invoke(context);
            instance = await machineInstanceRepository.CreateInstanceAsync(newCorrelationId, token);
        }

        var flow = statesWithFlows.FirstOrDefault(x => (State)x.State == new State(instance.State)).FlowOperator;
        if (flow is null)
            throw new StateMachineException.EventWasNotDeclaredForInstanceState(eventConfig.Event.GetType(),
                instance.State);

        var stateMachineContext = new StateMachineContext<TInstance, TMessage>
            (instance, context.Message, context.CorrelationId, context.Headers);
        foreach (var asyncAction in flow.AsyncActions) await asyncAction.Invoke(stateMachineContext, token);
        await machineInstanceRepository.SaveInstanceAsync(instance, token);
    }
}