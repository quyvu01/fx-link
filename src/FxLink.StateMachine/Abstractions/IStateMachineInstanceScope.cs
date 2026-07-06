namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineInstanceScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken token = default);
}