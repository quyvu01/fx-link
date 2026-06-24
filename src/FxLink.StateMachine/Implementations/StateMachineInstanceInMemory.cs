using System.Collections.Concurrent;
using System.Linq.Expressions;
using FxLink.Abstractions;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Implementations;

internal class StateMachineInstanceInMemory : IStateMachineInstancePersistence
{
    private readonly ConcurrentBag<object> _instances = [];
    
    public Task<TInstance> GetInstance<TInstance, TMessage>(
        Expression<Func<IConsumerContext<TMessage>, bool>> predicate)
        where TInstance : IStateMachineInstance where TMessage : class
    {
        throw new NotImplementedException();
    }

    public Task<TInstance> GetInstance<TInstance, TMessage>(
        Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> filter) where TInstance : IStateMachineInstance
        where TMessage : class
    {
        throw new NotImplementedException();
    }
}