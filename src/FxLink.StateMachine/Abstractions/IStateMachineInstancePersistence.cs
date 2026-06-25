using System.Linq.Expressions;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineInstancePersistence
{
    Task<TInstance> GetInstance<TInstance>(Expression<Func<TInstance, bool>> filter)
        where TInstance : IStateMachineInstance;

    Task<TInstance> CreateInstanceAsync<TInstance>(Guid correlationId, Expression<Func<TInstance, object>> stateSelector)
        where TInstance : IStateMachineInstance;
}