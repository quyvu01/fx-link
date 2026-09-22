using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IInboxConfigurator
{
    // Exposed so external backend packages (e.g. a future FxLink.Inbox.EntityFrameworkCore) can
    // register their own IInboxStore via extension methods on this interface, the same way
    // InMemoryInbox() does internally — mirrors IOutboxConfigurator.Services/Registry.
    IServiceCollection Services { get; }
    IInboxRegistry Registry { get; }

    void InMemoryInbox();
    void Options(Action<IInboxOptions> options = null);
    void MessageInbox<TMessage>([NotNull] Action<IMessageInboxConfigurator> options) where TMessage : class;
}
