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
}