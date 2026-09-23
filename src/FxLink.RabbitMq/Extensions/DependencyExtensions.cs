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
    extension(IConfigurator configurator)
    {
        public void AddRabbitMq(Action<IRabbitMqConfigurator> options)
        {
            var config = new RabbitMqConfigurator();
            options.Invoke(config);
            var services = configurator.Services;
            services.AddSingleton(config.ToConfiguration());
            services.AddSingleton<RabbitMqClient>();
            services.AddSingleton<IMessageBrokerConnector>(sp => sp.GetRequiredService<RabbitMqClient>());
            services.AddSingleton<IRabbitMqClient>(sp => sp.GetRequiredService<RabbitMqClient>());
            services.AddSingleton(typeof(IClientConnector<>), typeof(RabbitMqClientConnector<>));
            // IRequester<> is registered generically by AddFxLink itself (Requester<> wraps
            // whatever IClientConnector<> a transport provides) — RabbitMqClientConnector<> never
            // implemented IRequester<>, so a registration here would have been a no-op override
            // shadowed by AddFxLink's own later registration anyway. IWireResultDispatcher<>'s
            // implementation moved to core (FxLink.Implementations.WireResultDispatcher<>) and is
            // now registered by AddFxLink too, since its logic never depended on RabbitMq at all.
            services.AddHostedService<RabbitMqSupervisorWorker>();
        }

        /// <summary>
        /// Opts into delaying messages via the RabbitMQ delayed-message-exchange plugin — registers
        /// it as the IDelayMessageProvider. Only needed if you actually send messages with a delay;
        /// call a different transport/provider's own "Use...DelayScheduler()" to back delays with
        /// something else (Quartz, Hangfire, Redis, ...) instead.
        /// </summary>
        public void UseRabbitMqDelayScheduler() =>
            configurator.Services.AddSingleton<IDelayMessageProvider, RabbitMqScheduleExchangeProvider>();
    }
}