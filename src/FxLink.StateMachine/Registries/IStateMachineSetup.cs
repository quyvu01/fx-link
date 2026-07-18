using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Registries;

public interface IStateMachineSetup
{
    Type StateMachineType { get; }
    IServiceCollection Services { get; }
    void InMemoryRepository();
}