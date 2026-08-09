using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Delegates;

namespace FxLink.StateMachine.Registries;

public interface IMissingInstanceConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    IDispatcher<IConsumerContext<TMessage>> Discard();
    IDispatcher<IConsumerContext<TMessage>> Fault();
    IDispatcher<IConsumerContext<TMessage>> ExecuteAsync(MissingInstanceActionAsync<TMessage> actionAsync);
    IDispatcher<IConsumerContext<TMessage>> Execute(MissingInstanceAction<TMessage> action);
}