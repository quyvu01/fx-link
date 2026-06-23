using FxLink.Abstractions;
using FxLink.Delegates;
using FxLink.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.PipelineBehaviors;

internal class ConsumerPipelineBehaviorOrchestrator<TMessage>(IServiceProvider serviceProvider) where TMessage : class
{
    // Just lazy load pipeline behaviors and consumers because sometimes we want to defer services loaded on pipelines and consumers
    internal async Task ExecuteAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        var consumerData = serviceProvider.GetRequiredService<MessageMapConsumers>();
        if (!consumerData.Data.TryGetValue(typeof(TMessage), out var consumerKeys)) return;
        var consumerTasks = consumerKeys.Select(async key =>
        {
            using var scope = serviceProvider.CreateScope();
            var consumer = scope.ServiceProvider.GetKeyedService<IConsumer<TMessage>>(key);
            if (consumer is null) return;
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