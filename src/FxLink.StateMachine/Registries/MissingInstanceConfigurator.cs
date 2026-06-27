using FxLink.Abstractions;
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
    public IDispatch<IConsumerContext<TMessage>> Discard() => new Dispatch<IConsumerContext<TMessage>>(null);

    public IDispatch<IConsumerContext<TMessage>> Fault()
        => new Dispatch<IConsumerContext<TMessage>>((_, _) =>
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

    public IDispatch<IConsumerContext<TMessage>> ExecuteAsync(MissingInstanceActionAsync<TMessage> actionAsync) =>
        new Dispatch<IConsumerContext<TMessage>>(async (context, ct) =>
            await actionAsync.Invoke((IConsumerContext<TMessage>)context, ct));

    public IDispatch<IConsumerContext<TMessage>> Execute(MissingInstanceAction<TMessage> action) =>
        ExecuteAsync((context, _) =>
        {
            action.Invoke(context);
            return Task.CompletedTask;
        });
}