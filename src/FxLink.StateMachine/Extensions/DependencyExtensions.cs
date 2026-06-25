using FxLink.Extensions;
using FxLink.Registries;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Extensions;

public static class DependencyExtensions
{
    public static void AddStateMachines(this IConfigurator configurator, Action<IStateMachineConfigurator> options)
    {
        var stateMachineConfigurator = new StateMachineConfigurator(configurator.Services);
        var messageKeys = configurator.MessageKeys;
        options?.Invoke(stateMachineConfigurator);
        stateMachineConfigurator.MessageKeys
            .ForEach(mk => mk.Value
                .ForEach(v => messageKeys.AddMessageKey(mk.Key, v)));
    }
}