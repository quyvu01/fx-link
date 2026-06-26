using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Configurations;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Implementations.Workflows;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Implementations.StateMachines;

public abstract partial class StateMachine<TInstance> :
    IFlowInitialize,
    IFlowDefinition<TInstance>,
    IStateMachine
    where TInstance : IStateMachineInstance
{
    public IState Initial { get; } = new State(nameof(Initial));
    public IState Completed { get; } = new State(nameof(Completed));
    public IEnumerable<IState> States { get; }
    
    private readonly HashSet<EventConfiguration> _eventConfigurations = [];
    private readonly HashSet<IEvent> _eventTypes = [];
    protected IReadOnlyCollection<EventConfiguration> EventConfigurations => [.._eventConfigurations];
    private readonly ConcurrentDictionary<IState, List<IFlow>> _stateMapFlows = [];

    protected StateMachine()
    {
        States = [Initial, ..SetMachineStates(), Completed];
        SetEvents();
    }

    protected void Event<TMessage>(IEvent<TMessage> @event,
        [NotNull] Action<IEventConfigurator<TInstance, TMessage>> options) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(options);
        if (!_eventTypes.Add(@event))
            throw new StateMachineException.EventHasBeenConfiguration(typeof(TMessage));
        var config = new EventConfigurator<TInstance, TMessage>();
        options.Invoke(config);
        _eventConfigurations.Add(new EventConfiguration(@event, config));
    }
    
    // Todo, add validation for flows, we cannot add same events for flows of same state?
    public void Initially(params IFlow[] flows)
    {
        var existingFlows = _stateMapFlows.GetOrAdd(Initial, _ => []);
        existingFlows.AddRange(flows);
    }

    public void During(IState state, params IFlow[] flows)
    {
        var existingFlows = _stateMapFlows.GetOrAdd(state, _ => []);
        existingFlows.AddRange(flows);
    }

    public IFlowOperator<TInstance, TMessage> On<TMessage>(IEvent<TMessage> @event) where TMessage : class =>
        new FlowOperator<TInstance, TMessage>(@event);
}