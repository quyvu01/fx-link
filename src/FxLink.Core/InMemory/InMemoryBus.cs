using FxLink.Core.Abstractions;
using FxLink.Core.Contracts;
using FxLink.Core.PipelineBehaviors;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.InMemory;

internal sealed class InMemoryBus<TMessage>(IServiceProvider serviceProvider) :
    IClient<TMessage>,
    IServer<TMessage> where TMessage : class
{
    public async Task SendAsync(TMessage message, IContext context, CancellationToken token = default)
    {
        using var scope = serviceProvider.CreateScope();
        var consumerPipelineBehavior = scope.ServiceProvider
            .GetRequiredService<ConsumerPipelineBehaviorOrchestrator<TMessage>>();
        await consumerPipelineBehavior.ConsumeAsync(new ConsumerContext<TMessage>(message, context.CorrelationId,
            context.Headers), token);
    }

    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token)
    {
        using var scope = serviceProvider.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<IConsumer<TMessage>>();
        await consumer.ConsumeAsync(context, token);
    }
}