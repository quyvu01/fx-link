using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace FxLink.StateMachine.EntityFrameworkCore.Registries;

public interface IStateMachineEntityFrameworkConfigurator
{
    IsolationLevel IsolationLevel { get; set; }
    void UseConcurrencyMode([NotNull] Action<IConcurrencyModeConfigurator> options);
    void AddDbContext<TDbContext>() where TDbContext : DbContext;
    void DbContextFactory<TDbContext>(Func<TDbContext> dbContextFactory) where TDbContext : DbContext;
    void DbContextFactory<TDbContext>(Func<IServiceProvider, TDbContext> dbContextFactory) where TDbContext : DbContext;
}