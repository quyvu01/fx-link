using System.Diagnostics.CodeAnalysis;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.EntityFrameworkCore.Registries;
using FxLink.StateMachine.EntityFrameworkCore.Repositories;
using FxLink.StateMachine.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.EntityFrameworkCore.Extensions;

public static class DependencyExtensions
{
    public static void EntityFrameworkRepository(this IStateMachineSetup stateMachineSetup,
        [NotNull] Action<IStateMachineEntityFrameworkConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(stateMachineSetup);
        ArgumentNullException.ThrowIfNull(options);
        var configurator = new StateMachineEntityFrameworkConfigurator(stateMachineSetup.Services);
        options.Invoke(configurator);
        configurator.ValidateItSelf();
        stateMachineSetup.Services.AddSingleton(configurator.ToOptions());
        stateMachineSetup.Services.AddScoped(typeof(IStateMachineInstanceRepository<>),
            typeof(StateMachineInstanceRepository<>));
    }
}