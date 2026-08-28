using System.Diagnostics.CodeAnalysis;

namespace FxLink.Registries;

public interface IOutboxConfigurator
{
    void InMemoryOutbox();
    void DispatcherOptions(Action<IOutboxDispatcherOptions> options = null);
    void MessageOutbox<TMessage>([NotNull] Action<IMessageOutboxConfigurator> options) where TMessage : class;
}