using System.Reflection;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Exceptions;

namespace FxLink.StateMachine.Implementations.StateMachines;

// Todo: validate this one later, may we need to check more edge cases...
public abstract partial class StateMachine<TInstance>
{
    private void SetMachineStates()
    {
        var customStates = GetType()
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
        States = [Initial, ..customStates, Completed];
    }

    private void SetActivitiesInstance() => GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(t => typeof(IActivity).IsAssignableFrom(t.PropertyType) && t.PropertyType.IsInterface)
        .Where(p => p.PropertyType.IsGenericType)
        .ForEach(p =>
        {
            var activityType = p.PropertyType;
            var arg = activityType.GetGenericArguments()[0];
            var genericTypeDefinition = p.PropertyType.GetGenericTypeDefinition();
            try
            {
                if (genericTypeDefinition == typeof(IEvent<>))
                {
                    var eventInstance = Activator.CreateInstance(typeof(Event<>).MakeGenericType(arg));
                    p.SetValue(this, eventInstance);
                }

                if (genericTypeDefinition == typeof(ISchedule<>))
                {
                    var eventInstance = Activator.CreateInstance(typeof(Schedule<>).MakeGenericType(arg));
                    p.SetValue(this, eventInstance);
                }
            }
            catch (Exception)
            {
                throw new StateMachineException.EventIsNotCorrect(activityType);
            }
        });
}