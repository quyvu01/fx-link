using FxLink.Abstractions;
using FxLink.Abstractions.StateMachine;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Exceptions;
using FxLink.Implementations;

namespace FxLink.Registries.Implementations;

public class MissingInstanceConfigurator<TInstance, TMessage>
    : IMissingInstanceConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    // Silent, do nothing
    public IDispatcher<IConsumeContext<TMessage>> Discard() => new Dispatcher<IConsumeContext<TMessage>>(null);

    public IDispatcher<IConsumeContext<TMessage>> Fault()
        => new Dispatcher<IConsumeContext<TMessage>>((_, _) =>
        {
            try
            {
                throw new StateMachineException.MissingInstanceFaulted();
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        });

    public IDispatcher<IConsumeContext<TMessage>> ExecuteAsync(MissingInstanceActionAsync<TMessage> actionAsync) =>
        new Dispatcher<IConsumeContext<TMessage>>(async (context, ct) =>
            await actionAsync.Invoke((IConsumeContext<TMessage>)context, ct));

    public IDispatcher<IConsumeContext<TMessage>> Execute(MissingInstanceAction<TMessage> action) =>
        ExecuteAsync((context, _) =>
        {
            action.Invoke(context);
            return Task.CompletedTask;
        });
}
