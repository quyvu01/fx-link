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
    extension(IConfigurator distributedConfigurator)
    {
        public void AddRabbitMq(Action<IRabbitMqConfigurator> options)
        {
            var config = new RabbitMqConfigurator();
            options.Invoke(config);
            var services = distributedConfigurator.Services;
            services.AddSingleton(config.ToConfiguration());
            services.AddSingleton<RabbitMqClient>();
            services.AddSingleton<IMessageBrokerConnector>(sp => sp.GetRequiredService<RabbitMqClient>());
            services.AddSingleton<IRabbitMqClient>(sp => sp.GetRequiredService<RabbitMqClient>());
            services.AddSingleton(typeof(IClientConnector<>), typeof(RabbitMqClientConnector<>));
            services.AddSingleton(typeof(IRequester<>), typeof(RabbitMqClientConnector<>));
            services.AddSingleton(typeof(IWireResultDispatcher<>), typeof(WireResultDispatcher<>));
            services.AddSingleton(typeof(IConsumerConfiguratorResolver<>), typeof(ConsumerConfiguratorResolver<>));
            services.AddHostedService<RabbitMqSupervisorWorker>();
        }

        /// <summary>
        /// Opts into delaying messages via the RabbitMQ delayed-message-exchange plugin — registers
        /// it as the IDelayMessageProvider. Only needed if you actually send messages with a delay;
        /// call a different transport/provider's own "Use...DelayScheduler()" to back delays with
        /// something else (Quartz, Hangfire, Redis, ...) instead.
        /// </summary>
        public void UseRabbitMqDelayScheduler() =>
            distributedConfigurator.Services.AddSingleton<IDelayMessageProvider, RabbitMqScheduleExchangeProvider>();
    }
}