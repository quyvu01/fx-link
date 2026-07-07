using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IStateMachineActivityConfigurator<out TInstance, out TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    void Of<TStateMachineActivity>() where TStateMachineActivity : IStateMachineActivity<TInstance, TMessage>;
}