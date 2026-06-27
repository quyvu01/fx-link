using FxLink.Abstractions;
using FxLink.Delegates;
using FxLink.StateMachine.Exceptions;
using Microsoft.Extensions.Logging;

namespace FxLink.StateMachine.InternalPipelineBehaviors;

internal sealed class CatchStateMachinePipelineBehavior<TMessage>(
    ILogger<CatchStateMachinePipelineBehavior<TMessage>> logger)
    : IConsumerPipelineBehavior<TMessage> where TMessage : class
{
    public async Task ConsumeAsync(IConsumerContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        try
        {
            await next.Invoke(token);
        }
        catch (Exception e)
        {
            if (e is StateMachineException.StateMachineInstanceMustBeInitFirst)
            {
                logger.LogError("State machine instance must be initialized fist for context: {@Message}", context);
                // Do nothing here. We don't need to handle this exception!
            }

            throw;
        }
    }
}