using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.PipelineBehaviors;
using FxLink.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal class ServerConnector<TMessage>(
    IServiceProvider serviceProvider)
    : IServerConnector<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, Type consumerType,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(consumerType);
        using var scope = serviceProvider.CreateScope();
        ConsumerAmbient.SetConsumerAmbientData(scope.ServiceProvider, consumerType);
        var pipelineBehavior = scope.ServiceProvider
            .GetRequiredService<ConsumerPipelineBehaviorOrchestrator<TMessage>>();
        await pipelineBehavior.ExecuteAsync(context, token);
    }
}