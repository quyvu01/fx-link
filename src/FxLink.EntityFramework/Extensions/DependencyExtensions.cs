using System.Diagnostics.CodeAnalysis;
using FxLink.StateMachine.Abstractions;
using FxLink.EntityFramework.Registries;
using FxLink.EntityFramework.Repositories;
using FxLink.StateMachine.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.EntityFramework.Extensions;

public static class DependencyExtensions
{
    public static void EntityFrameworkRepository(this IStateMachineSetup stateMachineSetup,
        [NotNull] Action<IStateMachineEntityFrameworkConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(stateMachineSetup);
        ArgumentNullException.ThrowIfNull(options);
        var services = stateMachineSetup.Services;
        var configurator = new StateMachineEntityFrameworkConfigurator(stateMachineSetup, services);
        options.Invoke(configurator);
        configurator.ValidateItSelf();
        services.AddScoped(typeof(IStateMachineInstanceRepository<>), typeof(StateMachineInstanceRepository<>));
        stateMachineSetup.Services.AddKeyedSingleton<StateMachineEntityFrameworkOptions>(
            configurator.StateMachineInstanceType, configurator.ToOptions());
    }
}