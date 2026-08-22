using FxLink.Extensions;
using FxLink.Registries;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Implementations;
using FxLink.StateMachine.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.Extensions;

public static class DependencyExtensions
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