using System.Diagnostics.CodeAnalysis;
using FxLink.Extensions;
using FxLink.Registries;
using FxLink.RoutingSlip.Abstractions;
using FxLink.RoutingSlip.Implementations;
using FxLink.RoutingSlip.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.RoutingSlip.Extensions;

public static class DependencyExtensions
{
    public static void AddRoutingSlip(this IConfigurator configurator,
        [NotNull] Action<IRoutingSlipConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var services = configurator.Services;
        var routingSlipConfigurator = new RoutingSlipConfigurator(services);
        options.Invoke(routingSlipConfigurator);
        routingSlipConfigurator.Build();
        var messageKeys = configurator.MessageKeys();
        routingSlipConfigurator.MessageKeys
            .ForEach(mk => mk.Value
                .ForEach(v => messageKeys.AddMessageKey(mk.Key, v)));
        services.AddScoped<IRoutingSlipExecutor, RoutingSlipExecutor>();
        services.AddScoped(typeof(IRoutingSlipPublisher<>), typeof(RoutingSlipPublisher<>));
    }
}