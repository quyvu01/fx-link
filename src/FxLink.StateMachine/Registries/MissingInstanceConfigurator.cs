using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Exceptions;
using FxLink.Implementations;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Delegates;

namespace FxLink.StateMachine.Registries;

public class MissingInstanceConfigurator<TInstance, TMessage>
    : IMissingInstanceConfigurator<TInstance, TMessage>
    where TInstance : IStateMachineInstance where TMessage : class
{
    // Silent, do nothing
    public IDispatcher<IConsumerContext<TMessage>> Discard() => new Dispatcher<IConsumerContext<TMessage>>(null);

    public IDispatcher<IConsumerContext<TMessage>> Fault()
        => new Dispatcher<IConsumerContext<TMessage>>((_, _) =>
        {
            try
            {
                throw new DistributedException.FaultException();
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        });

    public IDispatcher<IConsumerContext<TMessage>> ExecuteAsync(MissingInstanceActionAsync<TMessage> actionAsync) =>
        new Dispatcher<IConsumerContext<TMessage>>(async (context, ct) =>
            await actionAsync.Invoke((IConsumerContext<TMessage>)context, ct));

    public IDispatcher<IConsumerContext<TMessage>> Execute(MissingInstanceAction<TMessage> action) =>
        ExecuteAsync((context, _) =>
        {
            action.Invoke(context);
            return Task.CompletedTask;
        });
}