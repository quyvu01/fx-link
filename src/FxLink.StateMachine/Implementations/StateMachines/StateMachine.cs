using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Configurations;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Implementations.Workflows;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Implementations.StateMachines;

public abstract partial class StateMachine<TInstance> :
    IFlowDefinition<TInstance>,
    IStateMachine
    where TInstance : IStateMachineInstance
{
    public IState Initial { get; } = new State(nameof(Initial));
    public IState Completed { get; } = new State(nameof(Completed));
    public IState[] States { get; }

    private readonly HashSet<EventConfiguration> _eventConfigurations = [];
    private readonly HashSet<IEvent> _eventTypes = [];
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

    protected void Initially(params IFlow[] flows) => BindingFlowsState(Initial, flows);

    protected void During(IState state, params IFlow[] flows) => BindingFlowsState(state, flows);

    protected void During(IState state1, IState state2, params IFlow[] flows)
        => During([state1, state2], flows);

    protected void During(IState state1, IState state2, IState state3, params IFlow[] flows)
        => During([state1, state2, state3], flows);

    protected void During(IState state1, IState state2, IState state3, IState state4, params IFlow[] flows)
        => During([state1, state2, state3, state4], flows);

    protected void During(IEnumerable<IState> states, params IFlow[] flows) =>
        states.ForEach(state => BindingFlowsState(state, flows));

    protected void DuringAny(params IFlow[] flows) => During(States, flows);

    public IFlowOperator<TInstance, TMessage> When<TMessage>(IEvent<TMessage> @event) where TMessage : class =>
        new FlowOperator<TInstance, TMessage>(@event);

    private void BindingFlowsState(IState state, IFlow[] flows)
    {
        var existingFlows = _stateMapFlows.GetOrAdd(state, _ => []);
        existingFlows.AddRange(flows);
    }
}