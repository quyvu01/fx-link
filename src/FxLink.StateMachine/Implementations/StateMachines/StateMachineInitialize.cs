using System.Reflection;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Exceptions;
using FxLink.StateMachine.Extensions;

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
                    throw new StateMachineException.StateDeclarationIsInvalid(state.PropertyType);
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
            var arg = activityType.GetGenericArguments();
            var genericTypeDefinition = p.PropertyType.GetGenericTypeDefinition();
            try
            {
                if (genericTypeDefinition == typeof(IEvent<>))
                {
                    var @event = (IActivity)Activator
                        .CreateInstance(typeof(Event<>).MakeGenericType(arg));
                    @event.SetName(p.Name);
                    p.SetValue(this, @event);
                    return;
                }

                if (genericTypeDefinition == typeof(ISchedule<>))
                {
                    var schedule = (IActivity)Activator
                        .CreateInstance(typeof(Schedule<>).MakeGenericType(arg));
                    schedule.SetName(p.Name);
                    if (schedule is IInternalEventConfigurator internalConfig) internalConfig.Configure();
                    p.SetValue(this, schedule);
                    return;
                }

                if (genericTypeDefinition == typeof(IRequest<,>))
                {
                    var request = (IActivity)Activator
                        .CreateInstance(typeof(Request<,>).MakeGenericType(arg));
                    request.SetName(p.Name);
                    if (request is IInternalEventConfigurator internalConfig) internalConfig.Configure();
                    p.SetValue(this, request);
                }
            }
            catch (Exception)
            {
                throw new StateMachineException.EventDeclarationIsInvalid(activityType);
            }
        });
}