using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.StateMachine.Abstractions;
using FxLink.Statics;

namespace FxLink.StateMachine.Implementations;

public sealed class StateMachineConsumer<TMessage>(IServiceProvider serviceProvider)
    : IConsumer<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, CancellationToken token = default)
    {
        var consumerType = ConsumerAmbient.ConsumerType;
        if (serviceProvider.GetService(consumerType) is IStateMachine stateMachine)
            await stateMachine.RaiseEventAsync(context, token);
    }
}