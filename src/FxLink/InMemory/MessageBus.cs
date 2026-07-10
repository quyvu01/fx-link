using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Exceptions;
using FxLink.Implementations;
using FxLink.PipelineBehaviors;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.InMemory;

internal sealed class MessageBus<TMessage> :
    IClientConnector<TMessage>,
    IServerConnector<TMessage>,
    IRequester<TMessage> where TMessage : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageProcessor<TMessage> _messageProcessor;
    private readonly ResponseInternal _responseInternal;

    public MessageBus(IServiceProvider serviceProvider, IMessageProcessor<TMessage> messageProcessor,
        ResponseInternal responseInternal)
    {
        _serviceProvider = serviceProvider;
        _messageProcessor = messageProcessor;
        _responseInternal = responseInternal;
        _ = Task.Run(async () =>
        {
            await foreach (var message in messageProcessor.MessagesProcessingAsync())
            {
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var server = scope.ServiceProvider.GetRequiredService<IServerConnector<TMessage>>();
                    Guid? requesterId = message.Context is IRequestContext rq ? rq.RequesterId : null;
                    await server.ConsumeAsync(
                        new ConsumerContext<TMessage>(message.Message, requesterId, message.Context), message.Token);
                });
                if (message.Context is IResponseContext responseContext)
                    _responseInternal.TrySetResult(responseContext.RequesterId, message);
            }
        });
    }

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
        => await _messageProcessor.PushMessageAsync(message, context, token);

    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var consumerPipelineBehavior = scope.ServiceProvider
            .GetRequiredService<ConsumerPipelineBehaviorOrchestrator<TMessage>>();
        await consumerPipelineBehavior.ExecuteAsync(context, token);
    }

    public async Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message, IRequestContext context,
        CancellationToken token = default) where TResponse : class
    {
        if (context.Timeout < TimeSpan.Zero)
            throw new FxLinkException.RequestTimeoutMustNotBeNegative(context.Timeout);
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(token);
        tcs.CancelAfter(context.Timeout);
        await SendAsync(message, context, tcs.Token);
        var (result, ctx, _) = await _responseInternal
            .GetResponse<Result>(context.RequesterId, tcs.Token);
        if (!result.IsSuccess) throw result.Fault.ToException();
        var response = result.Data as TResponse;
        return new ResponseContext<TResponse>(response, context.RequesterId, ctx);
    }

    public Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message,
        CancellationToken token = default)
        where TResponse : class
        => RequestAsync<TResponse>(message, new RequestContext(Guid.NewGuid(), []), token);
}