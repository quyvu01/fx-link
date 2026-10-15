using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Delegates;

namespace FxLink.StateMachine.Registries;

public interface IMissingInstanceConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    IDispatcher<IConsumeContext<TMessage>> Discard();
    IDispatcher<IConsumeContext<TMessage>> Fault();
    IDispatcher<IConsumeContext<TMessage>> ExecuteAsync(MissingInstanceActionAsync<TMessage> actionAsync);
    IDispatcher<IConsumeContext<TMessage>> Execute(MissingInstanceAction<TMessage> action);
}