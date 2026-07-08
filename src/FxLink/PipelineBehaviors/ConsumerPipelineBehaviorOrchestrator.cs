using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Delegates;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.PipelineBehaviors;

internal class ConsumerPipelineBehaviorOrchestrator<TMessage>(IServiceProvider serviceProvider) where TMessage : class
{
    // Just lazy load pipeline behaviors and consumers because sometimes we want to defer services loaded on pipelines and consumers
    internal async Task ExecuteAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        var messageKeys = serviceProvider.GetRequiredService<IMessageKeys>();
        var keys = messageKeys.GetKeysByMessageType(typeof(TMessage));
        var consumerTasks = keys.Select(async key =>
        {
            using var scope = serviceProvider.CreateScope();
            var consumer = scope.ServiceProvider.GetKeyedService<IConsumer<TMessage>>(key);
            if (consumer is null) return;
            ConsumerAmbient.SetConsumerAmbientData(scope.ServiceProvider, key as Type);
            var pipelineBehaviors = scope.ServiceProvider
                .GetServices<IConsumerPipelineBehavior<TMessage>>();
            var func = pipelineBehaviors
                .Reverse()
                .Aggregate((ConsumerHandlerDelegate)(ct => consumer.ConsumeAsync(context, ct)),
                    (acc, next) => ct => next.ConsumeAsync(context, acc, ct));
            await func.Invoke(token);
        });

        await Task.WhenAll(consumerTasks);
    }
}