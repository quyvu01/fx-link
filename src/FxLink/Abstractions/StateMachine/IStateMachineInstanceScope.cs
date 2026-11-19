namespace FxLink.Abstractions.StateMachine;

public interface IStateMachineInstanceScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken token = default);
}
