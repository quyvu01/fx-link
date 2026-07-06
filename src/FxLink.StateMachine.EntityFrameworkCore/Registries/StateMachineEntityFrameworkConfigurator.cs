using System.Data;
using System.Diagnostics.CodeAnalysis;
using FxLink.StateMachine.EntityFrameworkCore.Delegates;
using FxLink.StateMachine.EntityFrameworkCore.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.StateMachine.EntityFrameworkCore.Registries;

internal class StateMachineEntityFrameworkConfigurator(IServiceCollection services)
    : IStateMachineEntityFrameworkConfigurator
{
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.Serializable;
    public ConcurrencyMode ConcurrencyMode { get; set; }
    public SqlDialect? Dialect { get; set; }
    private Type _dbContextType;
    private Delegate _dbContextFactoryDelegate;

    public void UseConcurrencyMode([NotNull] Action<IConcurrencyModeConfigurator> options)
    {
        var config = new ConcurrencyModeConfigurator();
        options.Invoke(config);
        ConcurrencyMode = config.ConcurrencyMode;
        Dialect = config.Dialect;
    }

    public void AddDbContext<TDbContext>() where TDbContext : DbContext
    {
        _dbContextType = typeof(TDbContext);
        services.TryAddScoped<GetStateMachineInstance>(sp => sp.GetRequiredService<TDbContext>);
    }


    public void DbContextFactory<TDbContext>(Func<TDbContext> dbContextFactory) where TDbContext : DbContext
    {
        _dbContextFactoryDelegate = dbContextFactory;
        services.TryAddScoped<GetStateMachineInstance>(_ => dbContextFactory.Invoke);
    }

    public void DbContextFactory<TDbContext>(Func<IServiceProvider, TDbContext> dbContextFactory)
        where TDbContext : DbContext
    {
        _dbContextFactoryDelegate = dbContextFactory;
        services.TryAddScoped<GetStateMachineInstance>(sp => () => dbContextFactory.Invoke(sp));
    }

    internal void ValidateItSelf()
    {
        if (_dbContextType is not null && _dbContextFactoryDelegate is not null)
            throw new StateMachineEntityFrameworkCoreException.DbContextAlreadyConfigured(_dbContextType);
        if (_dbContextType is null && _dbContextFactoryDelegate is null)
            throw new StateMachineEntityFrameworkCoreException.DbContextNotConfigured();
        if (ConcurrencyMode is ConcurrencyMode.Pessimistic && Dialect is null)
            throw new StateMachineEntityFrameworkCoreException.PessimisticModeDialectNotConfigured();
    }

    internal StateMachineEntityFrameworkOptions ToOptions() => new(IsolationLevel, ConcurrencyMode, Dialect);
}