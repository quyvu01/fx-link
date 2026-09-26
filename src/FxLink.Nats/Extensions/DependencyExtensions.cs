using FxLink.Abstractions;
using FxLink.Nats.Abstractions;
using FxLink.Nats.BackgroundServices;
using FxLink.Nats.Implementations;
using FxLink.Nats.Registries;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Nats.Extensions;

public static class DependencyExtensions
{
    extension(IConfigurator configurator)
    {
        public void AddNats(Action<INatsConfigurator> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            var config = new NatsConfigurator();
            options.Invoke(config);
            var services = configurator.Services;
            services.AddSingleton(config.ToConfiguration());
            services.AddSingleton<NatsConnectionProvider>();
            services.AddSingleton<INatsConnectionProvider>(sp => sp.GetRequiredService<NatsConnectionProvider>());
            services.AddSingleton<NatsClient>();
            services.AddSingleton<IMessageBrokerConnector>(sp => sp.GetRequiredService<NatsClient>());
            services.AddSingleton<INatsMessagingClient>(sp => sp.GetRequiredService<NatsClient>());
            services.AddSingleton(typeof(IClientConnector<>), typeof(NatsClientConnector<>));
            services.AddHostedService<NatsSupervisorWorker>();
        }

        /// <summary>
        /// Opts into delaying messages via NATS message scheduling (server 2.12+) — registers it as
        /// the IDelayMessageProvider. Only needed for delayed publishes; retry backoff always uses
        /// scheduling and doesn't depend on this. Call a different provider's own
        /// "Use...DelayScheduler()" to back delays with something else instead.
        /// </summary>
        public void UseNatsDelayScheduler() =>
            configurator.Services.AddSingleton<IDelayMessageProvider, NatsDelayMessageProvider>();
    }
}
