using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.PipelineBehaviors;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal sealed class MessageBus<TMessage> :
    IClient<TMessage>,
    IServer<TMessage>,
    IRequest<TMessage> where TMessage : class
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
                    var server = scope.ServiceProvider.GetRequiredService<IServer<TMessage>>();
                    await server.ConsumeAsync(new ConsumerContext<TMessage>(message.Message,
                        message.Context.CorrelationId, message.Context.Headers), message.Token);
                });
                if (message.Context is IResponseContext)
                    _responseInternal.TrySetResult(message.Context.CorrelationId, message.Message);
            }
        });
    }

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
        => await _messageProcessor.PushMessageAsync(message, context, token);

    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token)
    {
        using var scope = _serviceProvider.CreateScope();
        var consumerPipelineBehavior = scope.ServiceProvider
            .GetRequiredService<ConsumerPipelineBehaviorOrchestrator<TMessage>>();
        await consumerPipelineBehavior.ExecuteAsync(context, token);
    }

    public async Task<TResponse> RequestAsync<TResponse>(TMessage message, IRequestContext context,
        CancellationToken token = default)
    {
        await SendAsync(message, context, token);
        return await _responseInternal.GetResponse<TResponse>(context.CorrelationId, token);
    }

    public Task<TResponse> RequestAsync<TResponse>(TMessage message, CancellationToken token = default)
        => RequestAsync<TResponse>(message, new RequestContext(Guid.NewGuid(), []), token);
}