using FxLink.StateMachine.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace FxLink.StateMachine.EntityFrameworkCore.Repositories;

// releaseLockAsync is only needed for dialects whose lock is not released implicitly by
// commit/rollback (e.g. MySQL's GET_LOCK, which is session-scoped, not transaction-scoped).
internal sealed class StateMachineInstanceScope(
    IDbContextTransaction transaction,
    Func<CancellationToken, Task> releaseLockAsync = null) : IStateMachineInstanceScope
{
    private bool _committed;

    public async Task CommitAsync(CancellationToken token = default)
    {
        await transaction.CommitAsync(token);
        _committed = true;
        if (releaseLockAsync is not null) await releaseLockAsync.Invoke(token);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            await transaction.RollbackAsync();
            if (releaseLockAsync is not null) await releaseLockAsync.Invoke(CancellationToken.None);
        }

        await transaction.DisposeAsync();
    }
}