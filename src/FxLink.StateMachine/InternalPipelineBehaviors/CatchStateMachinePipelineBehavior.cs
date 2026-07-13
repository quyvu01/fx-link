using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Delegates;
using FxLink.StateMachine.Exceptions;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.StateMachine.InternalPipelineBehaviors;

internal sealed class CatchStateMachinePipelineBehavior<TMessage>(
    IServiceProvider serviceProvider,
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
            switch (e)
            {
                case StateMachineException.InstanceMustBeInitializedFirst:
                    logger.LogError("State machine instance must be initialized fist for context: {@Message}", context);
                    // Do nothing here. We don't need to handle this exception!
                    break;
                case StateMachineException.EventNotDeclaredForState ex:
                    logger.LogError(ex.Message);
                    // We have to response Fault to requester. Seems we have to implement result pattern here
                    if (context.RequesterId is { } requesterId)
                    {
                        var client = serviceProvider.GetRequiredService<IClientConnector<Result>>();
                        await client.SendAsync(Result.Failed(ex), new ResponseContext(requesterId, context), token);
                    }

                    break;
            }
            throw;
        }
    }
}