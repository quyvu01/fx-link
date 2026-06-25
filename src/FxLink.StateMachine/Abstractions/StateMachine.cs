using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions.Workflows;
using FxLink.StateMachine.Configurations;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Implementations;
using FxLink.StateMachine.Implementations.Workflows;
using FxLink.StateMachine.Registries;
using FxLink.Statics;
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
    public IEnumerable<IState> States { get; }
    private Expression<Func<TInstance, object>> _stateSelector;
    private readonly HashSet<EventConfiguration> _eventConfigurations = [];
    private readonly HashSet<IEvent> _eventTypes = [];
    protected IReadOnlyCollection<EventConfiguration> EventConfigurations => [.._eventConfigurations];
    private readonly ConcurrentDictionary<IState, List<IFlow>> _stateMapFlows = [];

    protected StateMachine()
    {
        States = [Initial, ..SetMachineStates(), Completed];
        SetEvents();
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
    private void SetEvents() => GetType()
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
                var eventInstance = Activator.CreateInstance(typeof(Event<>).MakeGenericType(arg)) as Event;
                eventInstance?.SetName(p.Name);
                p.SetValue(this, eventInstance);
            }
            catch (Exception)
            {
                throw new StateMachineException.EventIsNotCorrect(eventType);
            }
        });

    // We will use this `InstanceState` to allocate instance state and change the state
    protected void State(Expression<Func<TInstance, object>> selector) => _stateSelector = selector;

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

    // Todo: this is very difficult logic, we have to verify very carefully. For now, just implement to have the full flow testing. 
    public async Task RaiseEventAsync<TMessage>(TMessage message, IContext context, CancellationToken token = default)
        where TMessage : class
    {
        var services = ServiceProviderAmbient.Services;
        var @event = new Event<TMessage>();
        var eventConfig = _eventConfigurations.FirstOrDefault(a => a.Event.Equals(@event));
        if (eventConfig?.Configurator is not IEventConfigurator<TInstance, TMessage> config) return;
        var consumerContext = new ConsumerContext<TMessage>(message, context.CorrelationId, context.Headers);
        var predicate = config.GetPredicate(consumerContext);
        var storage = services.GetRequiredService<IStateMachineInstancePersistence>();
        var instance = await storage.GetInstance(predicate);
        var states = _stateMapFlows.Where(v => v.Value
                .OfType<IFlowOperator<TInstance, TMessage>>()
                .Any(f => f.Event.Equals(@event)))
            .Select(x => new
            {
                State = x.Key,
                Flow = x.Value.OfType<IFlowOperator<TInstance, TMessage>>()
                    .First(k => k.Event.Equals(@event))
            })
            .ToArray();
        if (states is not { Length: > 0 })
            throw new StateMachineException.EventDoesNotMatchAnyFlow(typeof(IEvent<TMessage>));
        if (instance is null && states.All(x => x.State != Initial))
            throw new StateMachineException.StateMachineInstanceMustBeInitFirst();
        if (instance is null)
        {
            // It means that we already have the initial state => need to init a new instance.
            var correlationIdSelector = config.CorrelationIdSelector();
            var newCorrelationId = correlationIdSelector.Compile().Invoke(consumerContext);
            instance = await storage.CreateInstanceAsync(newCorrelationId, _stateSelector);
        }

        var initFlow = states.First(x => x.State == Initial).Flow;
        if (initFlow.Action is { } action) action.Invoke(instance, consumerContext);
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