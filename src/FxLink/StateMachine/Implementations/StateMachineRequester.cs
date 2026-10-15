using System.Threading.Channels;
using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Exceptions;
using FxLink.Extensions;
using FxLink.StateMachine.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Implementations;

internal class StateMachineRequester<TRequest> : IStateMachineRequester<TRequest>
    where TRequest : class
{
    private readonly IClientConnector<TRequest> _connector;
    private readonly IServiceProvider _serviceProvider;
    private readonly IInMemoryResponseGetter _inMemoryResponseGetter;
    private readonly Channel<Func<Task>> _messagePool = Channel.CreateUnbounded<Func<Task>>();

    public StateMachineRequester(IClientConnector<TRequest> connector,
        IServiceProvider serviceProvider,
        IInMemoryResponseGetter inMemoryResponseGetter)
    {
        _connector = connector;
        _serviceProvider = serviceProvider;
        _inMemoryResponseGetter = inMemoryResponseGetter;
        _ = Task.Run(async () =>
        {
            await foreach (var msg in _messagePool.Reader.ReadAllAsync()) msg.Invoke().Forget();
        });
    }

    public async Task RequestAsync<TResponse>(TRequest request, IRequestContext context,
        Func<IServiceProvider, TResponse, Task> succeedCallback,
        Func<IServiceProvider, TRequest, Exception, Task> faultCallback,
        Func<IServiceProvider, TRequest, Task> timeoutCallback,
        CancellationToken token = default) where TResponse : class
    {
        if (context.Timeout < TimeSpan.Zero)
            throw new FxLinkException.RequestTimeoutMustNotBeNegative(context.Timeout);
        await _connector.SendAsync(request, context, token);
        await _messagePool.Writer.WriteAsync(ResponseFunc, token);
        return;

        async Task ResponseFunc()
        {
            using var scoped = _serviceProvider.CreateScope();
            using var tcs = CancellationTokenSource.CreateLinkedTokenSource(token);
            tcs.CancelAfter(context.Timeout);
            try
            {
                var (result, _, _) = await _inMemoryResponseGetter
                    .GetResponse<TResponse>(context.RequesterId, tcs.Token);
                if (!result.IsSuccess)
                {
                    var exception = result.Fault.ToException();
                    await faultCallback.Invoke(scoped.ServiceProvider, request, exception);
                    return;
                }

                var response = result.Data;
                await succeedCallback.Invoke(scoped.ServiceProvider, response);
            }
            catch (TimeoutException)
            {
                await timeoutCallback.Invoke(scoped.ServiceProvider, request);
            }
            catch (Exception e)
            {
                await faultCallback.Invoke(scoped.ServiceProvider, request, e);
            }
        }
    }
}