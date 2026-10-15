using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Registries;

internal sealed class StateMachineSetup(IServiceCollection services, Type stateMachineType) : IStateMachineSetup
{
    public Type StateMachineType { get; } = stateMachineType;
    public IServiceCollection Services => services;

    public void InMemoryRepository()
    {
        services.AddSingleton(typeof(IStateMachineInstanceRepository<>),
            typeof(StateMachineInstanceInMemoryRepository<>));
    }
}