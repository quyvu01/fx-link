using FxLink.Abstractions;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachine
{
    IEnumerable<IState> States { get; }
    Task RaiseEventAsync<TMessage>(TMessage message, IContext context, CancellationToken token = default) where TMessage : class;
}