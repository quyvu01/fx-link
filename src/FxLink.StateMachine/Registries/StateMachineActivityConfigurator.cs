using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Exceptions;

namespace FxLink.StateMachine.Registries;

internal sealed class StateMachineActivityConfigurator<TInstance, TMessage>
    : IStateMachineActivityConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    internal Type ActivityOfType { get; private set; }
    internal Type ActivityOfInstanceType { get; private set; }

    public void OfType<TStateMachineActivity>()
        where TStateMachineActivity : IStateMachineActivity<TInstance, TMessage> =>
        ActivityOfType = typeof(TStateMachineActivity);

    public void OfInstanceType<TStateMachineActivity>()
        where TStateMachineActivity : IStateMachineActivity<TInstance> =>
        ActivityOfInstanceType = typeof(TStateMachineActivity);

    internal void ValidateItSelf()
    {
        if (ActivityOfType is null && ActivityOfInstanceType is null)
            throw new StateMachineException.ActivityTypeNotConfigured(typeof(TInstance), typeof(TMessage));
        if (ActivityOfType is not null && ActivityOfInstanceType is not null)
            throw new StateMachineException.ActivityTypeConfiguredTwice(typeof(TInstance), typeof(TMessage));
    }
}