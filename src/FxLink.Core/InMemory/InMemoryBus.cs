using FxLink.Core.Abstractions;
using FxLink.Core.Contracts;
using FxLink.Core.PipelineBehaviors;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.InMemory;

internal sealed class InMemoryBus<TMessage> :
    IClient<TMessage>,
    IServer<TMessage> where TMessage : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageProcessor<TMessage> _messageProcessor;

    public InMemoryBus(IServiceProvider serviceProvider, IMessageProcessor<TMessage> messageProcessor)
    {
        _serviceProvider = serviceProvider;
        _messageProcessor = messageProcessor;
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
            }
        });
    }

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        await _messageProcessor.PushMessageAsync(message, context, token);
    }

    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token)
    {
        using var scope = _serviceProvider.CreateScope();
        var consumerPipelineBehavior = scope.ServiceProvider
            .GetRequiredService<ConsumerPipelineBehaviorOrchestrator<TMessage>>();
        await consumerPipelineBehavior.ExecuteAsync(context, token);
    }
}