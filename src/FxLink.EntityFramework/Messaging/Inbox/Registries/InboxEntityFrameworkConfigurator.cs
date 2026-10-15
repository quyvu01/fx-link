using FxLink.Messaging.EntityFrameworkCore.Wrappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Messaging.EntityFrameworkCore.Inbox.Registries;

// registrationKey null = the default/unkeyed inbox (IInboxConfigurator.EntityFrameworkInbox);
// non-null = a per-TMessage inbox (IMessageInboxConfigurator.EntityFrameworkInbox), keyed by the
// message Type — same key EfInboxStore is registered under.
internal sealed class InboxEntityFrameworkConfigurator(IServiceCollection services, object registrationKey)
    : IInboxEntityFrameworkConfigurator
{
    private readonly DbContextRegistrar _registrar = new(services, registrationKey);

    public void AddDbContext<TDbContext>() where TDbContext : DbContext => _registrar.AddDbContext<TDbContext>();

    public void DbContextFactory<TDbContext>(Func<TDbContext> dbContextFactory) where TDbContext : DbContext =>
        _registrar.DbContextFactory(dbContextFactory);

    public void DbContextFactory<TDbContext>(Func<IServiceProvider, TDbContext> dbContextFactory)
        where TDbContext : DbContext => _registrar.DbContextFactory(dbContextFactory);

    internal void ValidateItSelf() => _registrar.ValidateItSelf();
}
