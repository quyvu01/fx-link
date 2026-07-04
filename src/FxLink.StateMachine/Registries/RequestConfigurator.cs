using FxLink.Faults;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

internal class RequestConfigurator<TInstance, TRequest, TResponse> :
    IRequestConfigurator<TInstance, TRequest, TResponse>
    where TInstance : IStateMachineInstance
    where TRequest : class
    where TResponse : class
{
    public TimeSpan Timeout { get; set; }
    public TimeSpan? TimeToLive { get; set; }

    public Action<IEventConfigurator<TInstance, TResponse>> Completed { get; set; }

    public Action<IEventConfigurator<TInstance, Fault<TRequest>>> Failed { get; set; }

    public Action<IEventConfigurator<TInstance, RequestTimeoutExpired<TRequest>>> TimeoutExpired { get; set; }

    // Fallback for Completed, Failed, TimeoutExpired should have the relation with requestId.
}