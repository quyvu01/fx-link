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
}