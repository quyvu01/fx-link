using System.Data;
using System.Linq.Expressions;

namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineInstanceRepository<TInstance> where TInstance : IStateMachineInstance
{
    Task<TInstance> GetInstanceAsync(Expression<Func<TInstance, bool>> filter, CancellationToken token = default);
    Task<Guid?> GetCorrelationIdAsync(Expression<Func<TInstance, bool>> filter, CancellationToken token = default);
    Task<TInstance> CreateInstanceAsync(Guid correlationId, CancellationToken token = default);
    Task SaveInstanceAsync(CancellationToken token = default);
    Task RemoveInstanceAsync(TInstance instance, CancellationToken token = default);
    Task<IStateMachineInstanceScope> BeginScopeAsync(Guid? correlationId, IsolationLevel? isolationLevel = null,
        CancellationToken token = default);
}