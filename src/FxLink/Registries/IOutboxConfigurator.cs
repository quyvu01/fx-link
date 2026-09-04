using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IOutboxConfigurator
{
    // Exposed so external backend packages (e.g. FxLink.Outbox.EntityFrameworkCore) can register
    // their own IOutboxStore/IPartitionLeaseStore via extension methods on this interface, the same
    // way InMemoryOutbox() does internally — mirrors IStateMachineSetup.Services.
    IServiceCollection Services { get; }
    IOutboxRegistry Registry { get; }

    void InMemoryOutbox();
    void DispatcherOptions(Action<IOutboxDispatcherOptions> options = null);
    void MessageOutbox<TMessage>([NotNull] Action<IMessageOutboxConfigurator> options) where TMessage : class;
}