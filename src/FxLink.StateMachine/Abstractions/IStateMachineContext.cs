using FxLink.Abstractions.Contexts;

namespace FxLink.StateMachine.Abstractions;

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