using FxLink.Contexts;
using FxLink.Exceptions;
using static FxLink.Configurators.DistributedConfigurators;

namespace FxLink.Extensions;

/// <summary>How many times the message being consumed has already been retried (0 on the first delivery).</summary>
internal sealed record RetryAttempt(int Count);

internal static class DeliveryControlExtensions
{
    /// <summary>
    /// Moves the retry bookkeeping a delivery arrived with (<c>x-retry-count</c>, and an
    /// <c>x-delivery-kind</c> of retry / dead-letter) out of the headers and into a payload.
    /// Everything a consumer publishes, requests or replies with is derived from the headers of the context
    /// it is consuming; left in place, a message that was retried would stamp those control headers onto
    /// unrelated downstream messages — e.g. routing them to the retry exchange of their own type.
    /// The retry behaviors read the count back through <see cref="GetRetryCount"/> and write the headers
    /// again only on the message they actually republish.
    /// </summary>
    internal static void CaptureDeliveryControl(this IConsumeContext context)
    {
        context.SetPayload(new RetryAttempt(context.Headers.Get<int>(Headers.RetryCountKey)));
        context.Headers.Set(Headers.RetryCountKey, null);

        var deliveryKind = context.Headers.Get<string>(Headers.DeliveryKindKey);
        if (deliveryKind is DeliveryKinds.Retry or DeliveryKinds.DeadLetter)
            context.Headers.Set(Headers.DeliveryKindKey, null);
    }

    internal static int GetRetryCount(this IConsumeContext context)
    {
        try
        {
            return context.GetPayload<RetryAttempt>().Count;
        }
        catch (FxLinkException.ContextPayloadNotFound)
        {
            // Not delivered through ConsumerConnector (e.g. built by hand), so nothing was captured.
            return context.Headers.Get<int>(Headers.RetryCountKey);
        }
    }
}
