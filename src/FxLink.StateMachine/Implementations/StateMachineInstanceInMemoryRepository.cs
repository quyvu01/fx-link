using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Helpers;
using FxLink.StateMachine.Implementations.StateMachines;

namespace FxLink.StateMachine.Implementations;

internal sealed class StateMachineInstanceInMemoryRepository<TInstance> : IStateMachineInstanceRepository<TInstance>
    where TInstance : IStateMachineInstance
{
    private readonly ConcurrentDictionary<Guid, TInstance> _instances = [];

    // No real DB/transaction backing this repository, so there is nothing to isolate or lock.
    public Task<IStateMachineInstanceScope> BeginScopeAsync(Guid? correlationId, IsolationLevel? isolationLevel = null,
        CancellationToken token = default) =>
        Task.FromResult<IStateMachineInstanceScope>(NoopStateMachineInstanceScope.Instance);

    public Task<TInstance> GetInstanceAsync(Expression<Func<TInstance, bool>> filter, CancellationToken token = default)
    {
        var instance = _instances
            .Values
            .FirstOrDefault(filter.Compile());
        return Task.FromResult(instance);
    }
    
    public Task<TInstance> CreateInstanceAsync(Guid correlationId, CancellationToken token = default)
    {
        var newInstance = InstanceUltilities.CreateInstance<TInstance>();
        newInstance.CorrelationId = correlationId;
        _instances.TryAdd(correlationId, newInstance);
        newInstance.State = nameof(StateMachine<>.Initial);

        return Task.FromResult(newInstance);
    }

    // No need to do because currently, we're using in memory -> instance will be changed by reference
    public Task SaveInstanceAsync(CancellationToken token = default) => Task.CompletedTask;

    public Task RemoveInstanceAsync(TInstance instance, CancellationToken token = default)
    {
        _instances.TryRemove(instance.CorrelationId, out _);
        return Task.CompletedTask;
    }
}