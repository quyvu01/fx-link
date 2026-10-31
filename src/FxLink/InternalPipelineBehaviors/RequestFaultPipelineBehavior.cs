using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Delegates;
using FxLink.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FxLink.InternalPipelineBehaviors;

/// <summary>
/// Owns the failure handling of request messages (request-as-publisher),
/// so that <see cref="RetryPipelineBehavior{TMessage}"/> never has to know about request/reply.
/// Registered right after the retry behavior, i.e. inside it: a failing request is absorbed here and the
/// retry behavior never sees the exception. The requester is bounded by its own timeout, so a retry could
/// land after it gave up, and rethrowing would only make the transport log it as critical and — with
/// RequeueOnFailureDelay enabled — redeliver the same failing request forever.
/// </summary>
internal sealed class RequestFaultPipelineBehavior<TMessage> : IConsumerPipelineBehavior<TMessage>
    where TMessage : class
{
    public async Task ConsumeAsync(IConsumeContext<TMessage> context, ConsumerHandlerDelegate next,
        CancellationToken token = default)
    {
        if (!context.IsRequestAsPublisher())
        {
            await next.Invoke(token);
            return;
        }

        try
        {
            await next.Invoke(token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            var logger = context.GetPayload<IServiceProvider>().GetService<ILogger<RequestFaultPipelineBehavior<TMessage>>>();
            logger?.LogError(ex,
                "Request message {@Message} failed; it is not retried or dead-lettered, the requester will time out",
                context.Message);
        }
    }
}
