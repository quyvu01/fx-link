using FxLink.Contexts;
using static FxLink.Configurators.DistributedConfigurators;

namespace FxLink.Extensions;

internal static class RequestSemanticsExtensions
{
    /// <summary>
    /// True when the message is a state-machine request: a caller is waiting on a reply (bounded by the
    /// request's own timeout), so broker-level retry / dead-lettering of it makes no sense.
    /// </summary>
    internal static bool IsRequestAsPublisher(this IContext context) =>
        context.Headers.Get<string>(Headers.RequestSemanticsKey) is RequestSemantics.RequestAsPublisher;
}
