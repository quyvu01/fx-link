using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Abstractions.Workflows;
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
    public IState[] States => _states.ToArray();
    private readonly HashSet<IState> _states = [];

    public IReadOnlyDictionary<IActivity, IActivityConfigurator> InternalActivityConfigurators
        => _internalActivityConfigurators;

    private readonly Dictionary<IActivity, IActivityConfigurator> _internalActivityConfigurators = [];
    private readonly Dictionary<IEvent, IActivityConfigurator> _messageConfigurators = [];
    private readonly ConcurrentDictionary<IState, List<IFlow>> _stateMapFlows = [];
    private bool _removeInstanceWhenCompleted;

    protected StateMachine()
    {
        SetMachineStates();
        SetActivitiesInstance();
    }

    // Activities setting up
    protected void Event<TMessage>([NotNull] IEvent<TMessage> @event,
        [NotNull] Action<IEventConfigurator<TInstance, TMessage>> options) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(options);
        var config = new EventConfigurator<TInstance, TMessage>();
        options.Invoke(config);
        if (!_messageConfigurators.TryAdd(@event, config))
            throw new StateMachineException.MessageTypeAlreadyConfigured(typeof(TMessage));
    }

    // Need more investigation with Internal behaviors
    protected void Schedule<TMessage>([NotNull] ISchedule<TMessage> schedule,
        [NotNull] Action<IScheduleConfigurator<TInstance, TMessage>> options) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(options);
        var config = new ScheduleConfigurator<TInstance, TMessage>();
        options.Invoke(config);
        config.ValidateItSelf();
        if (!_internalActivityConfigurators.TryAdd(schedule, config))
            throw new StateMachineException.ActivityAlreadyConfigured(typeof(TMessage));
        Event(schedule.Received, ev => config.Received.Invoke(ev));
    }

    protected void Request<TRequest, TResponse>([NotNull] IRequest<TRequest, TResponse> request,
        [NotNull] Action<IRequestConfigurator<TInstance, TRequest, TResponse>> options)
        where TRequest : class where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        var config = new RequestConfigurator<TInstance, TRequest, TResponse>();
        options.Invoke(config);
        if (!_internalActivityConfigurators.TryAdd(request, config))
            throw new StateMachineException.ActivityAlreadyConfigured(typeof(TRequest));
        Event(request.Completed, ev => config.Completed?.Invoke(ev));
        Event(request.Failed, ev => config.Failed?.Invoke(ev));
        Event(request.TimeoutExpired, ev => config.TimeoutExpired?.Invoke(ev));
        AddState(request.Pending);
    }


    // Starting flow chains.
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

    protected void RemoveInstanceWhenCompleted() => _removeInstanceWhenCompleted = true;

    public IFlowOperator<TInstance, TMessage> When<TMessage>(IEvent<TMessage> @event) where TMessage : class =>
        new FlowOperator<TInstance, TMessage>(@event, this);

    private void BindingFlowsState(IState state, IFlow[] flows) =>
        _stateMapFlows.GetOrAdd(state, _ => []).AddRange(flows);
}