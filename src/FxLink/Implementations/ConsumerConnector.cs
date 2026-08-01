using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.PipelineBehaviors;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal class ConsumerConnector<TMessage>(
    IServiceProvider serviceProvider)
    : IConsumerConnector<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, Type consumerType,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(consumerType);
        ConsumerAmbient.SetConsumerAmbientData(serviceProvider, consumerType);
        var pipelineBehavior = serviceProvider
            .GetRequiredService<ConsumerPipelineBehaviorOrchestrator<TMessage>>();
        await pipelineBehavior.ExecuteAsync(context, token);
    }
}