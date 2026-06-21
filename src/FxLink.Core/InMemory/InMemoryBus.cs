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
        messageProcessor.OnMessageProcessing(async (message, context, token) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var consumerPipelineBehavior = scope.ServiceProvider
                .GetRequiredService<ConsumerPipelineBehaviorOrchestrator<TMessage>>();
            await consumerPipelineBehavior.ExecuteAsync(new ConsumerContext<TMessage>(message, context.CorrelationId,
                context.Headers), token);
        });
    }

    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        await _messageProcessor.PushMessageAsync(message, context, token);
    }

    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token)
    {
        using var scope = _serviceProvider.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<IConsumer<TMessage>>();
        await consumer.ConsumeAsync(context, token);
    }
}