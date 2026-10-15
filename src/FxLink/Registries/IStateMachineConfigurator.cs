using System.Diagnostics.CodeAnalysis;
using FxLink.Abstractions;

namespace FxLink.Registries;

public interface IStateMachineConfigurator
{
    IStateMachineConfigurator Of<TStateMachine>([NotNull] Action<IStateMachineSetup> config)
        where TStateMachine : IStateMachine;

    IStateMachineConfigurator AddActivity<TStateMachineActivity>() where TStateMachineActivity : IStateMachineActivity;
    IStateMachineConfigurator AddActivity(Type stateMachineActivityType);
}
