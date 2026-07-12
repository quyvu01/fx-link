using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Exceptions;
using FxLink.Implementations;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.InMemory;

internal sealed class MessageBus<TMessage> :
    IClientConnector<TMessage>,
    IRequester<TMessage> where TMessage : class
{
    private readonly IMessageProcessor<TMessage> _messageProcessor;
    private readonly InMemoryResponseProcessor _inMemoryResponseProcessor;

    public MessageBus(IServiceProvider serviceProvider, IMessageProcessor<TMessage> messageProcessor,
        InMemoryResponseProcessor inMemoryResponseProcessor)
    {
        _messageProcessor = messageProcessor;
        _inMemoryResponseProcessor = inMemoryResponseProcessor;
        _ = Task.Run(async () =>
        {
            await foreach (var message in messageProcessor.MessagesProcessingAsync())
            {
                _ = Task.Run(async () =>
                {
                    using var scope = serviceProvider.CreateScope();
                    var services = scope.ServiceProvider;
                    var server = services.GetRequiredService<IServerConnector<TMessage>>();
                    Guid? requesterId = message.Context is IRequestContext rq ? rq.RequesterId : null;
                    var messageKeys = services.GetRequiredService<IMessageKeys>();
                    var consumerTypes = messageKeys.GetKeysByMessageType(typeof(TMessage));
                    var consumerContext = new ConsumerContext<TMessage>(message.Message, requesterId, message.Context);
                    var tasks = consumerTypes.Select(async c =>
                        await server.ConsumeAsync(consumerContext, c as Type, message.Token));
                    await Task.WhenAll(tasks);
                });
                if (message.Context is IResponseContext responseContext)
                    _inMemoryResponseProcessor.TrySetResult(responseContext.RequesterId, message);
            }
        });
    }

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
        => await _messageProcessor.PushMessageAsync(message, context, token);

    public async Task<IResponseContext<TResponse>> RequestAsync<TResponse>(TMessage message, IRequestContext context,
        CancellationToken token = default) where TResponse : class
    {
        if (context.Timeout < TimeSpan.Zero)
            throw new FxLinkException.RequestTimeoutMustNotBeNegative(context.Timeout);
        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(token);
        tcs.CancelAfter(context.Timeout);
        await SendAsync(message, context, tcs.Token);
        var (result, ctx, _) = await _inMemoryResponseProcessor
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