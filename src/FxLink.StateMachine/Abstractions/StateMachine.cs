using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using FxLink.StateMachine.Implementations;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Abstractions;

public abstract class StateMachine<TInstance> : IStateMachine where TInstance : IStateMachineInstance
{
    public IState Initial { get; } = new State(nameof(Initial));
    public IState Completed { get; } = new State(nameof(Completed));
    protected IEnumerable<IState> States { get; }

    protected StateMachine()
    {
        var states = SetMachineStates();
        States = [Initial, ..states, Completed];
    }

    // Todo: validate this one later, may we need to check more edge cases...
    private IEnumerable<IState> SetMachineStates() => GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(a => typeof(IState).IsAssignableFrom(a.PropertyType) && a.Name != nameof(Initial) &&
                    a.Name != nameof(Completed))
        .Select(state =>
        {
            state.SetValue(this, new State(state.Name));
            return state.GetValue(this);
        }).OfType<IState>();

    // We will use this `InstanceState` to allocate instance state and change the state
    protected void State<TProp>(Expression<Func<TInstance, TProp>> selector)
    {
    }

    protected void Event<TMessage>(IEvent<TMessage> @event,
        [NotNull] Action<IEventConfigurator<TInstance, TMessage>> options) where TMessage : class
    {
    }
}