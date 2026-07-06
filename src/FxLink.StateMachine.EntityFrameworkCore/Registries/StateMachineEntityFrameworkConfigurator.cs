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
    private IsolationLevel _isolationLevel = IsolationLevel.Serializable;
    private ConcurrencyMode _concurrencyMode = ConcurrencyMode.Optimistic;
    private SqlDialect? _dialect;
    private Type _dbContextType;
    private Delegate _dbContextFactoryDelegate;

    public void SetIsolationLevel(IsolationLevel isolationLevel) => _isolationLevel = isolationLevel;

    public void UseConcurrencyMode([NotNull] Action<IConcurrencyModeConfigurator> options)
    {
        var config = new ConcurrencyModeConfigurator();
        options.Invoke(config);
        _concurrencyMode = config.ConcurrencyMode;
        _dialect = config.Dialect;
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
        if (_concurrencyMode is ConcurrencyMode.Pessimistic && _dialect is null)
            throw new StateMachineEntityFrameworkCoreException.PessimisticModeDialectNotConfigured();
    }

    internal StateMachineEntityFrameworkOptions ToOptions() => new(_isolationLevel, _concurrencyMode, _dialect);
}