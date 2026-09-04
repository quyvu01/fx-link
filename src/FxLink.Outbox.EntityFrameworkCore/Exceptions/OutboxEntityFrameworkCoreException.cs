using FxLink.Exceptions;
using FxLink.Outbox.EntityFrameworkCore.Registries;

namespace FxLink.Outbox.EntityFrameworkCore.Exceptions;

/// <summary>
/// Groups the exceptions thrown by FxLink.Outbox.EntityFrameworkCore (DbContext registration).
/// </summary>
public static class OutboxEntityFrameworkCoreException
{
    /// <summary>AddDbContext/DbContextFactory was called more than once for the same setup.</summary>
    public sealed class DbContextAlreadyConfigured(Type dbContextType) :
        DistributedException(
            $"Add {dbContextType.Name} is invoked multiple times. Calling {nameof(IOutboxEntityFrameworkConfigurator.AddDbContext)} or {nameof(IOutboxEntityFrameworkConfigurator.DbContextFactory)} only one times");

    /// <summary>Neither AddDbContext nor DbContextFactory was called during setup.</summary>
    public sealed class DbContextNotConfigured()
        : DistributedException(
            $"DbContext must be added by calling {nameof(IOutboxEntityFrameworkConfigurator.AddDbContext)} or {nameof(IOutboxEntityFrameworkConfigurator.DbContextFactory)}");
}
