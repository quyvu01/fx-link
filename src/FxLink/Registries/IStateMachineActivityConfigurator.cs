using FxLink.Abstractions;

namespace FxLink.Registries;

public interface IStateMachineActivityConfigurator<out TInstance>
    where TInstance : IStateMachineInstance
{
    void OfInstanceType<TStateMachineActivity>() where TStateMachineActivity : IStateMachineActivity<TInstance>;
}

public interface IStateMachineActivityConfigurator<out TInstance, out TMessage> :
    IStateMachineActivityConfigurator<TInstance>
    where TInstance : IStateMachineInstance where TMessage : class
{
    void OfType<TStateMachineActivity>() where TStateMachineActivity : IStateMachineActivity<TInstance, TMessage>;
}
