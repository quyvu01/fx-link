using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Configurations;
using FxLink.StateMachine.Contexts;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Implementations;
using FxLink.StateMachine.Implementations.Workflows;
using FxLink.StateMachine.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Abstractions;

public abstract class StateMachine<TInstance> :
    IFlowInitialize,
    IFlowDefinition<TInstance>,
    IStateMachine
    where TInstance : IStateMachineInstance
{
    public IState Initial { get; } = new State(nameof(Initial));
    public IState Completed { get; } = new State(nameof(Completed));
    protected IEnumerable<IState> States { get; }
    private Expression _stateSelector;
    private readonly HashSet<EventConfiguration> _eventConfigurations = [];
    private readonly HashSet<IEvent> _eventTypes = [];
    protected IReadOnlyCollection<EventConfiguration> EventConfigurations => [.._eventConfigurations];
    private readonly ConcurrentDictionary<IState, List<IFlow>> _stateMapFlows = [];

    protected StateMachine()
    {
        States = [Initial, ..SetMachineStates(), Completed];
        InitEvents();
    }

    // Todo: validate this one later, may we need to check more edge cases...
    private IEnumerable<IState> SetMachineStates() => GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(a => typeof(IState).IsAssignableFrom(a.PropertyType) && a.Name != nameof(Initial) &&
                    a.Name != nameof(Completed))
        .Select(state =>
        {
            try
            {
                state.SetValue(this, new State(state.Name));
                return state.GetValue(this);
            }
            catch (Exception)
            {
                throw new StateMachineException.StateConfigurationIsNotCorrect(state.PropertyType);
            }
        }).OfType<IState>();

    // Todo: validate this one later, may we need to check more edge cases...
    private void InitEvents() => GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(t => typeof(IEvent).IsAssignableFrom(t.PropertyType) && t.PropertyType.IsInterface)
        .Where(t => t.PropertyType.IsGenericType && t.PropertyType
            .GetGenericTypeDefinition() == typeof(IEvent<>))
        .ForEach(p =>
        {
            var eventType = p.PropertyType;
            try
            {
                var arg = eventType.GetGenericArguments()[0];
                var eventInstance = Activator.CreateInstance(typeof(Event<>).MakeGenericType(arg), p.Name);
                p.SetValue(this, eventInstance);
            }
            catch (Exception)
            {
                throw new StateMachineException.EventIsNotCorrect(eventType);
            }
        });

    // We will use this `InstanceState` to allocate instance state and change the state
    protected void State<TProp>(Expression<Func<TInstance, TProp>> selector) => _stateSelector = selector;

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

    public async Task RaiseEventAsync<TMessage>(IEvent<TMessage> @event) where TMessage : class
    {
        // Start the chain
        // 1. Find the relevant flows, if one event is occured on multiple flows, how can we get all flow?
        // we just map the state to flows, we don't know which flow we need to start?
        // We need to check if we have any instance 1st. 
        var services = ContextAmbient.GetServices();
        var storage = services.GetRequiredService<IStateMachineInstancePersistence>();
        var eventConfig = _eventConfigurations.FirstOrDefault(a => a.Event == @event);
        if (eventConfig is null) return; // Temporary return(early)
        var config = eventConfig.Configurator;
    }

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