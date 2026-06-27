using System.Collections.Concurrent;
using System.Linq.Expressions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations.StateMachines;

namespace FxLink.StateMachine.Implementations;

internal class StateMachineInstanceInMemoryRepository<TInstance> : IStateMachineInstanceRepository<TInstance>
    where TInstance : IStateMachineInstance
{
    private readonly ConcurrentBag<object> _instances = [];

    public Task<TInstance> GetInstanceAsync(Expression<Func<TInstance, bool>> filter, CancellationToken token = default)
    {
        var instance = _instances.OfType<TInstance>()
            .Where(filter.Compile())
            .FirstOrDefault();
        return Task.FromResult(instance);
    }

    // Todo: Check the best way to init a new StateMachine instance. Or we can put a validation like new().
    // Also, we have to check the Initial state, this is the very simple example to set the state(to test only)
    // We need to have more scenario for state setting like enum, int, string or immutable object?
    public Task<TInstance> CreateInstanceAsync(Guid correlationId, CancellationToken token = default)
    {
        var newInstance = Activator.CreateInstance<TInstance>()!;
        _instances.Add(newInstance);
        newInstance.CorrelationId = correlationId;
        newInstance.State = nameof(StateMachine<>.Initial);

        return Task.FromResult(newInstance);
    }

    // No need to do because currently, we're using in memory -> instance will be changed by reference
    public Task SaveInstanceAsync(TInstance instance, CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
}