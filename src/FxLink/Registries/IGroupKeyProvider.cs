using FxLink.Contexts;

namespace FxLink.Registries;

public interface IGroupKeyProvider
{
    // Escape hatch for callers that only know TMessage (e.g. BatchAccumulator<TMessage>) and can't
    // know the TKey chosen by whoever called GroupBy<TProperty>() at registration time. Concrete
    // providers implement this explicitly, casting context back to IConsumeContext<TMessage> and
    // boxing the typed key.
    bool TryGetKey(object context, out object key);
}

public interface IGroupKeyProvider<in TMessage, TKey> : IGroupKeyProvider where TMessage : class
{
    bool TryGetKey(IConsumeContext<TMessage> context, out TKey key);
}