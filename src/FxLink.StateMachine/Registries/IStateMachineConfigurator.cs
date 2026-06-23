using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Registries;

public interface IStateMachineConfigurator
{
    IStateMachineConfigurator Of<TStateMachine>(Action<IStateMachineSetup> config = null) where TStateMachine : IStateMachine;
}