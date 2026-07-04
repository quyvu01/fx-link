using FxLink.Faults;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Extensions;

namespace FxLink.StateMachine.Implementations;

internal sealed record Request<TRequest, TResponse> : IRequest<TRequest, TResponse>, IInternalEventConfigurator
    where TRequest : class where TResponse : class
{
    public string Name { get; set; } = typeof(TRequest).Name;
    public IEvent<TResponse> Completed { get; } = new Event<TResponse>();
    public IEvent<Fault<TRequest>> Failed { get; } = new Event<Fault<TRequest>>();

    public IEvent<RequestTimeoutExpired<TRequest>> TimeoutExpired { get; } =
        new Event<RequestTimeoutExpired<TRequest>>();

    public IState Pending { get; private set; }

    public void Configure()
    {
        Completed.SetName($"{Name}.{nameof(Completed)}");
        Failed.SetName($"{Name}.{nameof(Failed)}");
        TimeoutExpired.SetName($"{Name}.{nameof(TimeoutExpired)}");
        Pending = new State($"{Name}.{nameof(Pending)}");
    }
}