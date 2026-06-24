using FxLink.StateMachine.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Registries;

public sealed class StateMachineConfigurator(IServiceCollection services) : IStateMachineConfigurator
{
    public IStateMachineConfigurator Of<TStateMachine>(Action<IStateMachineSetup> config = null) where TStateMachine : IStateMachine
    {
        // Todo: check later because I'm so tired right now
        services.AddSingleton(typeof(TStateMachine));
        
        var stateMachineSetup = new StateMachineSetup(services);
        config?.Invoke(stateMachineSetup);
        return this;
    }
}