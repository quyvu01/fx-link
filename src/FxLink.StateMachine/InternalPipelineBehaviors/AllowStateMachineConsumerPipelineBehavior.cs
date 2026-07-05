using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Delegates;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Contexts;
using FxLink.Statics;

namespace FxLink.StateMachine.InternalPipelineBehaviors;

internal class AllowStateMachineConsumerPipelineBehavior<TMessage> : IConsumerPipelineBehavior<TMessage>
    where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        if (context is not StateMachineConsumerContext<TMessage>)
        {
            await next.Invoke(token);
            return;
        }

        if (!typeof(IStateMachine).IsAssignableFrom(ConsumerAmbient.ConsumerType)) return;
        await next.Invoke(token);
    }
}