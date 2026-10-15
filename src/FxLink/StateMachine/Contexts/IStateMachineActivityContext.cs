using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Contexts;

public interface IStateMachineActivityContext<out TInstance> : IStateMachineContext<TInstance>
    where TInstance : IStateMachineInstance
{
    void TranslationTo(string state);
}

public interface IStateMachineActivityContext<out TInstance, out TMessage> :
    IStateMachineContext<TInstance, TMessage>,
    IStateMachineActivityContext<TInstance>
    where TInstance : IStateMachineInstance where TMessage : class;