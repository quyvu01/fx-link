using FxLink.Contexts;

namespace FxLink.Registries;

internal class GroupKeyProvider<TMessage, TKey>(Func<IConsumeContext<TMessage>, TKey> selector)
    : IGroupKeyProvider<TMessage, TKey>
    where TMessage : class where TKey : class
{
    public bool TryGetKey(IConsumeContext<TMessage> context, out TKey key)
    {
        key = selector.Invoke(context);
        return key != null;
    }

    bool IGroupKeyProvider.TryGetKey(object context, out object key)
    {
        if (context is not IConsumeContext<TMessage> typedContext)
        {
            key = null;
            return false;
        }

        var found = TryGetKey(typedContext, out var typedKey);
        key = typedKey;
        return found;
    }
}