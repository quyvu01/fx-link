using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Implementations;

internal sealed class NoopStateMachineInstanceScope : IStateMachineInstanceScope
{
    public static readonly NoopStateMachineInstanceScope Instance = new();

    private NoopStateMachineInstanceScope()
    {
    }

    public Task CommitAsync(CancellationToken token = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}