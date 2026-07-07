using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IStateMachineConfigurator
{
    IStateMachineConfigurator Of<TStateMachine>([NotNull] Action<IStateMachineSetup> config)
        where TStateMachine : IStateMachine;

    IStateMachineConfigurator AddActivity<TStateMachineActivity>() where TStateMachineActivity : IStateMachineActivity;
    IStateMachineConfigurator AddActivity(Type stateMachineActivityType);
    IStateMachineConfigurator AddActivitiesFromAssembly<TAssembly>();
    IStateMachineConfigurator AddActivitiesFromAssemblies(params Assembly[] assemblies);
}