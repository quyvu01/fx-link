using System.Diagnostics.CodeAnalysis;
using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IStateMachineConfigurator
{
    IStateMachineConfigurator Of<TStateMachine>([NotNull] Action<IStateMachineSetup> config)
        where TStateMachine : IStateMachine;
}