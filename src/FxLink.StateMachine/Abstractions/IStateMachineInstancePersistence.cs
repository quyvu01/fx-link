using System.Linq.Expressions;
using FxLink.Abstractions;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineInstancePersistence
{
    Task<TInstance> GetInstance<TInstance, TMessage>(Expression<Func<IConsumerContext<TMessage>, bool>> predicate)
        where TMessage : class where TInstance : IStateMachineInstance;

    Task<TInstance> GetInstance<TInstance, TMessage>(
        Expression<Func<TInstance, IConsumerContext<TMessage>, bool>> filter)
        where TMessage : class where TInstance : IStateMachineInstance;
}