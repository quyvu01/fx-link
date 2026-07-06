using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Registries;

public interface IStateMachineSetup
{
    IServiceCollection Services { get; }
    void InMemoryRepository();
}