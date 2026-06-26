using System.Collections.Concurrent;
using System.Linq.Expressions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations.StateMachines;

namespace FxLink.StateMachine.Implementations;

internal class StateMachineInstanceInMemory : IStateMachineInstancePersistence
{
    private readonly ConcurrentBag<object> _instances = [];

    public Task<TInstance> GetInstanceAsync<TInstance>(Expression<Func<TInstance, bool>> filter)
        where TInstance : IStateMachineInstance
    {
        var instance = _instances.OfType<TInstance>()
            .Where(filter.Compile())
            .FirstOrDefault();
        return Task.FromResult(instance);
    }

    // Todo: Check the best way to init a new StateMachine instance. Or we can put a validation like new().
    // Also, we have to check the Initial state, this is the very simple example to set the state(to test only)
    // We need to have more scenario for state setting like enum, int, string or immutable object?
    public Task<TInstance> CreateInstanceAsync<TInstance>(Guid correlationId)
        where TInstance : IStateMachineInstance
    {
        var newInstance = (TInstance)Activator.CreateInstance(typeof(TInstance))!;
        _instances.Add(newInstance);
        newInstance.CorrelationId = correlationId;
        newInstance.State = nameof(StateMachine<>.Initial);

        return Task.FromResult(newInstance);
    }
}