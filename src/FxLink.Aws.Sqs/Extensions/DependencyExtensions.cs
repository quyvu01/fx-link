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
                credential.ServiceUrlValue));
            services.AddSingleton<ISqsConnection, SqsConnection>();
            services.AddSingleton<SqsClient>();
            services.AddSingleton<IMessageBrokerConnector>(sp => sp.GetRequiredService<SqsClient>());
            services.AddSingleton<ISqsClient>(sp => sp.GetRequiredService<SqsClient>());
            services.AddSingleton(typeof(IClientConnector<>), typeof(SqsClientConnector<>));
            // Request/response (IRequester<>) isn't implemented yet for this transport — see
            // SqsClientConnector<TMessage>.SendAsync's NotSupportedException guard.
            services.AddHostedService<SqsSupervisorWorker>();
        }
    }
}