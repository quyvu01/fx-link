using Microsoft.EntityFrameworkCore;

namespace FxLink.Messaging.EntityFrameworkCore.Outbox.Registries;

public interface IOutboxEntityFrameworkConfigurator
{
    void AddDbContext<TDbContext>() where TDbContext : DbContext;
    void DbContextFactory<TDbContext>(Func<TDbContext> dbContextFactory) where TDbContext : DbContext;
    void DbContextFactory<TDbContext>(Func<IServiceProvider, TDbContext> dbContextFactory) where TDbContext : DbContext;
}
