using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Registries;

public sealed class StateMachineSetup(IServiceCollection services) : IStateMachineSetup
{
    public IServiceCollection Services => services;

    public void InMemoryRepository()
    {
        services.AddSingleton(typeof(IStateMachineInstanceRepository<>),
            typeof(StateMachineInstanceInMemoryRepository<>));
    }
}