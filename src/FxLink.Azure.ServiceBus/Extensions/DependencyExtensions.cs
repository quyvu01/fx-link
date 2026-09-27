using FxLink.Abstractions;
using FxLink.Azure.ServiceBus.Abstractions;
using FxLink.Azure.ServiceBus.BackgroundServices;
using FxLink.Azure.ServiceBus.Implementations;
using FxLink.Azure.ServiceBus.Registries;
using FxLink.Azure.ServiceBus.Topology;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Azure.ServiceBus.Extensions;

public static class DependencyExtensions
{
    extension(IConfigurator configurator)
    {
        public void AddServiceBus(Action<IServiceBusConfigurator> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            var config = new ServiceBusConfigurator();
            options.Invoke(config);
            var services = configurator.Services;
            services.AddSingleton(config.ToConfiguration());
            services.AddSingleton<ServiceBusConnectionProvider>();
            services.AddSingleton<IServiceBusConnectionProvider>(sp =>
                sp.GetRequiredService<ServiceBusConnectionProvider>());
            services.AddSingleton<ServiceBusTopologyBuilder>();
            services.AddSingleton<ServiceBusProvisioner>();
            services.AddSingleton<IServiceBusTopologyExporter, ServiceBusTopologyExporter>();
            services.AddSingleton<ServiceBusTransportClient>();
            services.AddSingleton<IMessageBrokerConnector>(sp => sp.GetRequiredService<ServiceBusTransportClient>());
            services.AddSingleton<IServiceBusMessagingClient>(sp =>
                sp.GetRequiredService<ServiceBusTransportClient>());
            services.AddSingleton(typeof(IClientConnector<>), typeof(ServiceBusClientConnector<>));
            services.AddHostedService<ServiceBusSupervisorWorker>();
        }

        /// <summary>
        /// Opts into delaying messages via Service Bus scheduled messages (no upper limit) — registers it as the
        /// IDelayMessageProvider. Only needed for delayed publishes; retry backoff always uses scheduling and doesn't
        /// depend on this. Call a different provider's own "Use...DelayScheduler()" to back delays with something
        /// else instead.
        /// </summary>
        public void UseServiceBusDelayScheduler() =>
            configurator.Services.AddSingleton<IDelayMessageProvider, ServiceBusDelayMessageProvider>();
    }
}
