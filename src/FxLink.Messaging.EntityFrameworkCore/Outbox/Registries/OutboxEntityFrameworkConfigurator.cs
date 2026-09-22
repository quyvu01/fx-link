using FxLink.Messaging.EntityFrameworkCore.Wrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Messaging.EntityFrameworkCore.Outbox.Registries;

// registrationKey null = the default/unkeyed outbox (IOutboxConfigurator.EntityFrameworkOutbox);
// non-null = a per-TMessage outbox (IMessageOutboxConfigurator.EntityFrameworkOutbox), keyed by
// the message Type — same key EfOutboxStore/EfPartitionLeaseStore are registered under.
internal sealed class OutboxEntityFrameworkConfigurator(IServiceCollection services, object registrationKey)
    : IOutboxEntityFrameworkConfigurator
{
    private readonly DbContextRegistrar _registrar = new(services, registrationKey);

    public void AddDbContext<TDbContext>() where TDbContext : DbContext => _registrar.AddDbContext<TDbContext>();

    public void DbContextFactory<TDbContext>(Func<TDbContext> dbContextFactory) where TDbContext : DbContext =>
        _registrar.DbContextFactory(dbContextFactory);

    public void DbContextFactory<TDbContext>(Func<IServiceProvider, TDbContext> dbContextFactory)
        where TDbContext : DbContext => _registrar.DbContextFactory(dbContextFactory);

    internal void ValidateItSelf() => _registrar.ValidateItSelf();
}
