using System.Linq.Expressions;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineInstanceRepository<TInstance> where TInstance : IStateMachineInstance
{
    Task<TInstance> GetInstanceAsync(Expression<Func<TInstance, bool>> filter, CancellationToken token = default);
    Task<TInstance> CreateInstanceAsync(Guid correlationId, CancellationToken token = default);
    Task SaveInstanceAsync(TInstance instance, CancellationToken token = default);
}