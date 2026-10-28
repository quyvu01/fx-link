using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FxLink.EntityFramework.Tests.TestSupport;

// Runs a callback once, right before the first SaveChanges hits the database — used to simulate
// another instance winning an insert race, or a database failure that isn't a duplicate key.
internal sealed class RunBeforeSaveInterceptor(Func<Task> beforeSave) : SaveChangesInterceptor
{
    private bool _ran;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_ran) return result;
        _ran = true;
        await beforeSave();
        return result;
    }
}
