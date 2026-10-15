using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;

namespace FxLink.Registries;

public interface IMissingInstanceConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    IDispatcher<IConsumeContext<TMessage>> Discard();
    IDispatcher<IConsumeContext<TMessage>> Fault();
    IDispatcher<IConsumeContext<TMessage>> ExecuteAsync(MissingInstanceActionAsync<TMessage> actionAsync);
    IDispatcher<IConsumeContext<TMessage>> Execute(MissingInstanceAction<TMessage> action);
}
