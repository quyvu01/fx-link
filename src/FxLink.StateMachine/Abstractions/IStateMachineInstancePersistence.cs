using System.Linq.Expressions;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineInstancePersistence
{
    Task<TInstance> GetInstanceAsync<TInstance>(Expression<Func<TInstance, bool>> filter)
        where TInstance : IStateMachineInstance;

    Task<TInstance> CreateInstanceAsync<TInstance>(Guid correlationId) where TInstance : IStateMachineInstance;
}