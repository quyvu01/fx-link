using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.InMemory;

internal sealed class InMemoryClientConnector<TMessage> :
    IClientConnector<TMessage> where TMessage : class
{
    private readonly IInMemoryMessageProcessor<TMessage> _inMemoryMessageProcessor;

    public InMemoryClientConnector(IServiceProvider serviceProvider,
        IInMemoryMessageProcessor<TMessage> inMemoryMessageProcessor,
        IInMemoryResponseSetter memoryResponseSetter)
    {
        _inMemoryMessageProcessor = inMemoryMessageProcessor;
        _ = Task.Run(async () =>
        {
            await foreach (var message in inMemoryMessageProcessor.MessagesProcessingAsync())
            {
                _ = Task.Run(async () =>
                {
                    using var scope = serviceProvider.CreateScope();
                    var services = scope.ServiceProvider;
                    var server = services.GetRequiredService<IConsumerConnector<TMessage>>();
                    Guid? requesterId = message.Context is IRequestContext rq ? rq.RequesterId : null;
                    var messageKeys = services.GetRequiredService<IMessageKeys>();
                    var consumerTypes = messageKeys.GetKeysByMessageType(typeof(TMessage));
                    var consumerContext = new ConsumerContext<TMessage>(message.Message, requesterId, message.Context);
                    var tasks = consumerTypes.Select(async c =>
                        await server.ConsumeAsync(consumerContext, c, message.Token));
                    await Task.WhenAll(tasks);
                });
                if (message.Context is IResponseContext responseContext)
                    memoryResponseSetter.TrySetResult(responseContext.RequesterId, message);
            }
        });
    }

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
        => await _inMemoryMessageProcessor.PushMessageAsync(message, context, token);
}