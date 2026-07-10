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
        services.AddSingleton<RabbitmqConnector>();
        services.AddSingleton<IMessageBrokerConnector>(sp => sp.GetRequiredService<RabbitmqConnector>());
        services.AddSingleton<IPublishMessage>(sp => sp.GetRequiredService<RabbitmqConnector>());
        services.AddSingleton<IRequestMessage>(sp => sp.GetRequiredService<RabbitmqConnector>());
        services.AddSingleton<IConsumeMessage>(sp => sp.GetRequiredService<RabbitmqConnector>());
        services.AddSingleton(typeof(IMessageProcessor<>), typeof(RabbitMqMessageProcessor<>));

        services.AddHostedService<RabbitMqSupervisorWorker>();
    }
}