using FxLink.Outbox.EntityFrameworkCore.Exceptions;
using FxLink.Outbox.EntityFrameworkCore.Wrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Outbox.EntityFrameworkCore.Registries;

// registrationKey null = the default/unkeyed outbox (IOutboxConfigurator.EntityFrameworkOutbox);
// non-null = a per-TMessage outbox (IMessageOutboxConfigurator.EntityFrameworkOutbox), keyed by
// the message Type — same key EfOutboxStore/EfPartitionLeaseStore are registered under.
internal sealed class OutboxEntityFrameworkConfigurator(IServiceCollection services, object registrationKey)
    : IOutboxEntityFrameworkConfigurator
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

    internal void ValidateItSelf()
    {
        if (_dbContextConfigurationCallCount > 1)
            throw new OutboxEntityFrameworkCoreException.DbContextAlreadyConfigured(_dbContextType);
        if (_dbContextConfigurationCallCount == 0)
            throw new OutboxEntityFrameworkCoreException.DbContextNotConfigured();
    }
}