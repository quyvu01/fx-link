using FxLink.Messaging.EntityFrameworkCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Messaging.EntityFrameworkCore.Wrappers;

// Shared AddDbContext/DbContextFactory/ValidateItSelf boilerplate behind both
// IOutboxEntityFrameworkConfigurator and IInboxEntityFrameworkConfigurator — registrationKey null
// means the default/unkeyed store, non-null means a per-TMessage store keyed by the message Type
// (same key the corresponding IOutboxStore/IInboxStore is registered under).
internal sealed class DbContextRegistrar(IServiceCollection services, object registrationKey)
{
    private Type _dbContextType;
    private int _dbContextConfigurationCallCount;

    public void AddDbContext<TDbContext>() where TDbContext : DbContext
    {
        _dbContextType = typeof(TDbContext);
        _dbContextConfigurationCallCount++;
        RegisterWrapped(sp => new DbContextWrapped(sp.GetRequiredService<TDbContext>()));
    }

    public void DbContextFactory<TDbContext>(Func<TDbContext> dbContextFactory) where TDbContext : DbContext
    {
        _dbContextType = typeof(TDbContext);
        _dbContextConfigurationCallCount++;
        RegisterWrapped(_ => new DbContextWrapped(dbContextFactory.Invoke()));
    }

    public void DbContextFactory<TDbContext>(Func<IServiceProvider, TDbContext> dbContextFactory)
        where TDbContext : DbContext
    {
        _dbContextType = typeof(TDbContext);
        _dbContextConfigurationCallCount++;
        RegisterWrapped(sp => new DbContextWrapped(dbContextFactory.Invoke(sp)));
    }

    private void RegisterWrapped(Func<IServiceProvider, DbContextWrapped> factory)
    {
        if (registrationKey is null)
        {
            services.AddScoped(factory);
            return;
        }

        services.AddKeyedScoped(registrationKey, (sp, _) => factory(sp));
    }

    public void ValidateItSelf()
    {
        if (_dbContextConfigurationCallCount > 1)
            throw new MessagingEntityFrameworkCoreException.DbContextAlreadyConfigured(_dbContextType);
        if (_dbContextConfigurationCallCount == 0)
            throw new MessagingEntityFrameworkCoreException.DbContextNotConfigured();
    }
}
