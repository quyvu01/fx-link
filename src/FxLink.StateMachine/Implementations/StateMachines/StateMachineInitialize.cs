using System.Reflection;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Exceptions;

namespace FxLink.StateMachine.Implementations.StateMachines;

// Todo: validate this one later, may we need to check more edge cases...
public abstract partial class StateMachine<TInstance>
{
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
}