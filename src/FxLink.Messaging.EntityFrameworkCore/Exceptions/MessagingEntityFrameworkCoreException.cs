using FxLink.Exceptions;

namespace FxLink.Messaging.EntityFrameworkCore.Exceptions;

/// <summary>
/// Groups the exceptions thrown by the shared DbContext-registration boilerplate behind both the
/// Outbox and Inbox EF Core backends.
/// </summary>
public static class MessagingEntityFrameworkCoreException
{
    /// <summary>AddDbContext/DbContextFactory was called more than once for the same setup.</summary>
    public sealed class DbContextAlreadyConfigured(Type dbContextType) :
        DistributedException(
            $"Add {dbContextType.Name} is invoked multiple times. Calling AddDbContext or DbContextFactory only one times");

    /// <summary>Neither AddDbContext nor DbContextFactory was called during setup.</summary>
    public sealed class DbContextNotConfigured()
        : DistributedException("DbContext must be added by calling AddDbContext or DbContextFactory");
}
