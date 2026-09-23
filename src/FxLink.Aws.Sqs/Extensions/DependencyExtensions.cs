using FxLink.Abstractions;
using FxLink.Aws.Sqs.Abstractions;
using FxLink.Aws.Sqs.BackgroundServices;
using FxLink.Aws.Sqs.Implementations;
using FxLink.Aws.Sqs.Registries;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Aws.Sqs.Extensions;

public static class DependencyExtensions
{
    extension(IConfigurator configurator)
    {
        public void AddSqs(Action<ISqsConfigurator> options)
        {
            var config = new SqsConfigurator();
            options.Invoke(config);
            var services = configurator.Services;
            var credential = (SqsCredential)config.Credential;
            services.AddSingleton<ISqsConfiguration>(new SqsConfiguration(
                credential.AccessKeyIdValue,
                credential.SecretAccessKeyValue,
                config.AwsRegionValue,
                credential.ServiceUrlValue,
                config.MaxReceiveCountValue));
            services.AddSingleton<ISqsConnection, SqsConnection>();
            services.AddSingleton<SqsClient>();
            services.AddSingleton<IMessageBrokerConnector>(sp => sp.GetRequiredService<SqsClient>());
            services.AddSingleton<ISqsClient>(sp => sp.GetRequiredService<SqsClient>());
            services.AddSingleton(typeof(IClientConnector<>), typeof(SqsClientConnector<>));
            // Request/response (IRequester<>) isn't implemented yet for this transport — see
            // SqsClientConnector<TMessage>.SendAsync's NotSupportedException guard.
            services.AddHostedService<SqsSupervisorWorker>();
        }

        /// <summary>
        /// Opts into delaying messages via SQS's native SendMessageRequest.DelaySeconds (capped
        /// at 15 minutes) — registers it as the IDelayMessageProvider. Only needed if you actually
        /// send messages with a delay; call a different transport/provider's own
        /// "Use...DelayScheduler()" to back delays with something else (EventBridge Scheduler,
        /// Quartz, Hangfire, Redis, ...) instead.
        /// </summary>
        public void UseSqsDelayScheduler() =>
            configurator.Services.AddSingleton<IDelayMessageProvider, SqsDelayMessageProvider>();
    }
}