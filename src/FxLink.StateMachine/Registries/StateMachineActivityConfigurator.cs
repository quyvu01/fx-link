using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

internal sealed class StateMachineActivityConfigurator<TInstance, TMessage>
    : IStateMachineActivityConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    internal Type StateMachineActivityType { get; private set; }

    public void Of<TStateMachineActivity>() where TStateMachineActivity : IStateMachineActivity<TInstance, TMessage> =>
        StateMachineActivityType = typeof(TStateMachineActivity);
}