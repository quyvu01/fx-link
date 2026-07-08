using FxLink.Abstractions;
using FxLink.RabbitMq.Abstractions;
using FxLink.RabbitMq.BackgroundServices;
using FxLink.RabbitMq.Implementations;
using FxLink.RabbitMq.Registries;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.RabbitMq.Extensions;

public static class DependencyExtensions
{
    public static void AddRabbitMq(this IConfigurator distributedConfigurator,
        Action<IRabbitMqConfigurator> options)
    {
        var config = new RabbitMqConfigurator();
        options.Invoke(config);
        var services = distributedConfigurator.Services;
        services.AddSingleton(config.ToConfiguration());
        services.AddSingleton<IMessageBrokerConnector, RabbitMqServer>();
        services.AddSingleton<IPublishMessage>(sp =>
            sp.GetRequiredService<IMessageBrokerConnector>() as RabbitMqServer);
        services.AddSingleton<IRequestMessage>(sp =>
            sp.GetRequiredService<IMessageBrokerConnector>() as RabbitMqServer);
        services.AddSingleton<IConsumeMessage>(sp =>
            sp.GetRequiredService<IMessageBrokerConnector>() as RabbitMqServer);
        services.AddSingleton(typeof(IMessageProcessor<>), typeof(RabbitMqMessageProcessor<>));
        // Use RabbitMqSupervisorWorker with supervisor pattern
        services.AddHostedService<RabbitMqSupervisorWorker>();
    }
}