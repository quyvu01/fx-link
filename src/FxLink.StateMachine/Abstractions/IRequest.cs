using FxLink.Faults;

namespace FxLink.StateMachine.Abstractions;

public interface IRequest<TRequest, TResponse> : IActivity where TRequest : class where TResponse : class
{
    IEvent<TResponse> Completed { get; }
    IEvent<Fault<TRequest>> Failed { get; }
    IEvent<RequestTimeoutExpired<TRequest>> TimeoutExpired { get; }
    IState Pending { get; }
}