using FxLink.Contexts;

namespace FxLink.Registries;

public interface IGroupKeyProvider;

public interface IGroupKeyProvider<in TMessage, TKey> : IGroupKeyProvider where TMessage : class
{
    bool TryGetKey(IConsumeContext<TMessage> context, out TKey key);
}