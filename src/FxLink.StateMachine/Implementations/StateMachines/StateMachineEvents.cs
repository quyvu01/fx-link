using FxLink.Abstractions;
using FxLink.Contexts;
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
    public async Task RaiseEventAsync<TMessage>(TMessage message, IContext context, CancellationToken token = default)
        where TMessage : class
    {
        var services = ServiceProviderAmbient.Services;
        var @event = new Event<TMessage>();
        var eventConfig = _eventConfigurations.FirstOrDefault(a => a.Event.Equals(@event));
        if (eventConfig?.Configurator is not EventConfigurator<TInstance, TMessage> config) return;
        var consumerContext = new ConsumerContext<TMessage>(message, context.CorrelationId, context.Headers);
        var predicate = config.GetPredicate(consumerContext);
        var storage = services.GetRequiredService<IStateMachineInstanceRepository>();
        var instance = await storage.GetInstanceAsync(predicate);

        var statesWithFlows = _stateMapFlows.Where(v => v.Value
                .OfType<IFlowOperator<TInstance, TMessage>>()
                .Any(f => f.Event.Equals(@event)))
            .Select(x => new
            {
                State = x.Key,
                Flow = x.Value.OfType<IFlowOperator<TInstance, TMessage>>()
                    .First(k => k.Event.Equals(@event))
            })
            .ToArray();
        if (statesWithFlows is not { Length: > 0 })
            throw new StateMachineException.EventDoesNotMatchAnyFlow(typeof(IEvent<TMessage>));
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
                await missingInvocationResult.SendAsync(consumerContext, token);
                return;
            }

            // It means that we already have the initial state => need to init a new instance.
            var correlationIdSelector = config.CorrelationSelector;
            var newCorrelationId = correlationIdSelector.Compile().Invoke(consumerContext);
            instance = await storage.CreateInstanceAsync<TInstance>(newCorrelationId);
        }

        var flow = statesWithFlows.First(x => (State)x.State == new State(instance.State)).Flow;

        foreach (var asyncAction in flow.AsyncActions)
            await asyncAction.Invoke(
                new StateMachineContext<TInstance, TMessage>(instance, message, context.CorrelationId, context.Headers),
                token);
    }
}