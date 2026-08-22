using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.PipelineBehaviors;

internal class ConsumerPipelineBehaviorOrchestrator<TMessage>(IServiceProvider serviceProvider) where TMessage : class
{
    internal async Task ExecuteAsync(IConsumeContext<TMessage> context, CancellationToken token = default)
    {
        var consumerType = context.GetPayload<ConsumerContextWrapped>().ConsumerType;
        if (consumerType is null)
        {
            var messageKeys = serviceProvider.GetRequiredService<IMessageKeys>();
            consumerType = messageKeys.GetKeysByMessageType(typeof(TMessage))
                .FirstOrDefault();
        }

        // A batch consumer is registered as IConsumer<IBatch<TMessage>>, keyed under the same
        // consumerType — but TMessage here is still the ORIGINAL (unwrapped) wire type (see
        // Configurator.AddConsumer), so GetKeyedService<IConsumer<TMessage>> below would never
        // find it. Route into the accumulator instead; it re-enters this same orchestrator, closed
        // over IBatch<TMessage>, once a batch is ready (see BatchDispatcher).
        if (serviceProvider.GetKeyedService<IBatchAccumulator<TMessage>>(consumerType) is { } accumulator)
        {
            await accumulator.AddAsync(context, token);
            return;
        }

        var consumer = serviceProvider.GetKeyedService<IConsumer<TMessage>>(consumerType);
        if (consumer is null) return;
        var pipelineBehaviors = serviceProvider
            .GetServices<IConsumerPipelineBehavior<TMessage>>();
        var func = pipelineBehaviors
            .Reverse()
            .Aggregate((ConsumerHandlerDelegate)(ct => consumer.ConsumeAsync(context, ct)),
                (acc, next) => ct => next.ConsumeAsync(context, acc, ct));
        await func.Invoke(token);
    }
}