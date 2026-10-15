using FxLink.Faults;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IRequestConfigurator<TInstance, TRequest, TResponse> : IActivityConfigurator
    where TInstance : IStateMachineInstance
    where TRequest : class
    where TResponse : class
{
    TimeSpan Timeout { get; set; }
    TimeSpan? TimeToLive { get; set; }
    Action<IEventConfigurator<TInstance, TResponse>> Completed { get; set; }
    Action<IEventConfigurator<TInstance, Fault<TRequest>>> Failed { get; set; }
    Action<IEventConfigurator<TInstance, RequestTimeoutExpired<TRequest>>> TimeoutExpired { get; set; }
}