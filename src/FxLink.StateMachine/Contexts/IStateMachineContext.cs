using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Contexts;

public interface IStateMachineContext<out TInstance> : IContextPayload, IContext where TInstance : IStateMachineInstance
{
    TInstance Instance { get; }
    Guid? RequesterId { get; }
}

public interface IStateMachineContext<out TInstance, out TMessage> : IStateMachineContext<TInstance>
    where TInstance : IStateMachineInstance where TMessage : class
{
    TMessage Message { get; }
}