using System.Data;
using System.Diagnostics.CodeAnalysis;
using FxLink.Extensions;
using FxLink.StateMachine.EntityFrameworkCore.Exceptions;
using FxLink.StateMachine.EntityFrameworkCore.Wrappers;
using FxLink.StateMachine.Implementations.StateMachines;
using FxLink.StateMachine.Registries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.EntityFrameworkCore.Registries;

internal class StateMachineEntityFrameworkConfigurator : IStateMachineEntityFrameworkConfigurator
{
    private IsolationLevel _isolationLevel = IsolationLevel.Serializable;
    private ConcurrencyMode _concurrencyMode = ConcurrencyMode.Optimistic;
    private SqlDialect? _dialect;
    private Type _dbContextType;
    private int _dbContextConfigurationCallCount;
    internal readonly Type StateMachineInstanceType;
    private readonly IServiceCollection _sevices;

    public StateMachineEntityFrameworkConfigurator(IStateMachineSetup stateMachineSetup, IServiceCollection services)
    {
        _sevices = services;
        var stateMachineBaseType = stateMachineSetup.StateMachineType
            .GetGenericBaseType(typeof(StateMachine<>));
        if (stateMachineBaseType is null) return;
        StateMachineInstanceType = stateMachineBaseType.GetGenericArguments().First();
    }

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
        _dbContextConfigurationCallCount++;
        _sevices.AddKeyedScoped(StateMachineInstanceType,
            (sp, _) => new DbContextWrapped(sp.GetRequiredService<TDbContext>()));
    }


    public void DbContextFactory<TDbContext>(Func<TDbContext> dbContextFactory) where TDbContext : DbContext
    {
        _dbContextType = typeof(TDbContext);
        _dbContextConfigurationCallCount++;
        _sevices.AddKeyedScoped(StateMachineInstanceType,
            (_, _) => new DbContextWrapped(dbContextFactory.Invoke()));
    }

    public void DbContextFactory<TDbContext>(Func<IServiceProvider, TDbContext> dbContextFactory)
        where TDbContext : DbContext
    {
        _dbContextType = typeof(TDbContext);
        _dbContextConfigurationCallCount++;
        _sevices.AddKeyedScoped(StateMachineInstanceType,
            (sp, _) => new DbContextWrapped(dbContextFactory.Invoke(sp)));
    }

    internal void ValidateItSelf()
    {
        if (_dbContextConfigurationCallCount > 1)
            throw new StateMachineEntityFrameworkCoreException.DbContextAlreadyConfigured(_dbContextType);
        if (_dbContextConfigurationCallCount == 0)
            throw new StateMachineEntityFrameworkCoreException.DbContextNotConfigured();
        if (_concurrencyMode is ConcurrencyMode.Pessimistic && _dialect is null)
            throw new StateMachineEntityFrameworkCoreException.PessimisticModeDialectNotConfigured();
    }

    internal StateMachineEntityFrameworkOptions ToOptions() => new(_isolationLevel, _concurrencyMode, _dialect);
}