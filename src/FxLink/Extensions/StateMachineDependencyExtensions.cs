using FxLink.Extensions;
using FxLink.Registries;
using FxLink.Abstractions;
using FxLink.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Extensions;

public static class StateMachineDependencyExtensions
{
    public static void AddStateMachines(this IConfigurator configurator, Action<IStateMachineConfigurator> options)
    {
        var stateMachineConfigurator = new StateMachineConfigurator(configurator.Services);
        options?.Invoke(stateMachineConfigurator);
        var messageKeys = configurator.MessageKeys();
        stateMachineConfigurator.MessageKeys
            .ForEach(mk => mk.Value
                .ForEach(v => messageKeys.AddMessageKey(mk.Key, v)));
        configurator.Services.AddSingleton(typeof(IStateMachineRequester<>), typeof(StateMachineRequester<>));
    }
}
