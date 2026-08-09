using FxLink.Abstractions;
using FxLink.Contexts;

namespace FxLink.Extensions;

internal static class InternalContextExtensions
{
    internal static void SetContext(this IMessageAction publisher, IContext context)
    {
        if (publisher is IInternalContext internalContext) internalContext.SetContext(context);
    }
}