using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Delegates;

namespace FxLink.StateMachine.Registries;

public interface IMissingInstanceConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    IDispatch<IConsumerContext<TMessage>> Discard();
    IDispatch<IConsumerContext<TMessage>> Fault();
    IDispatch<IConsumerContext<TMessage>> ExecuteAsync(MissingInstanceActionAsync<TMessage> actionAsync);
    IDispatch<IConsumerContext<TMessage>> Execute(MissingInstanceAction<TMessage> action);
}