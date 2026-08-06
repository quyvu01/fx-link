using FxLink.Abstractions.Contexts;

namespace FxLink.StateMachine.Abstractions;

internal interface IStateMachineRequester<TRequest> where TRequest : class
{
    Task RequestAsync<TResponse>(TRequest request, IRequestContext context,
        Func<IServiceProvider, TResponse, Task> succeedCallback,
        Func<IServiceProvider, TRequest, Exception, Task> faultCallback,
        Func<IServiceProvider, TRequest, Task> timeoutCallback,
        CancellationToken token = default);
}