using FxLink.Contexts;

namespace FxLink.Registries;

internal sealed class ValueTypeGroupByProvider<TMessage, TKey>(Func<IConsumeContext<TMessage>, TKey?> selector)
    : IGroupKeyProvider<TMessage, TKey> where TMessage : class where TKey : struct
{
    public bool TryGetKey(IConsumeContext<TMessage> context, out TKey key)
    {
        var property = selector.Invoke(context);
        if (property.HasValue)
        {
            key = property.Value;
            return true;
        }

        key = default;
        return false;
    }

    bool IGroupKeyProvider.TryGetKey(object context, out object key)
    {
        if (context is not IConsumeContext<TMessage> typedContext)
        {
            key = null;
            return false;
        }

        var found = TryGetKey(typedContext, out var typedKey);
        key = found ? typedKey : null;
        return found;
    }
}