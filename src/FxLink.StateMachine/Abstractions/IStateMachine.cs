using FxLink.Abstractions;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachine
{
    IState[] States { get; }

    Task RaiseEventAsync<TMessage>(IConsumerContext<TMessage> context, CancellationToken token = default)
        where TMessage : class;
}