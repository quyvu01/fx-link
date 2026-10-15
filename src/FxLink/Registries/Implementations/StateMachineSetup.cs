using FxLink.Abstractions;
using FxLink.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

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
