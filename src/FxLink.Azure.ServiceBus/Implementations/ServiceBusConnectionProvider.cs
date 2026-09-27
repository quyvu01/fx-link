using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using FxLink.Azure.ServiceBus.Abstractions;
using FxLink.Azure.ServiceBus.Registries;

namespace FxLink.Azure.ServiceBus.Implementations;

internal sealed class ServiceBusConnectionProvider : IServiceBusConnectionProvider, IAsyncDisposable
{
    public ServiceBusClient Client { get; }
    public ServiceBusAdministrationClient Administration { get; }

    public ServiceBusConnectionProvider(IServiceBusConfiguration configuration)
    {
        if (configuration.ConnectionString is { } connectionString)
        {
            Client = new ServiceBusClient(connectionString);
            Administration = new ServiceBusAdministrationClient(connectionString);
            return;
        }

        var credential = configuration.Credential ?? new DefaultAzureCredential();
        Client = new ServiceBusClient(configuration.FullyQualifiedNamespace, credential);
        Administration = new ServiceBusAdministrationClient(configuration.FullyQualifiedNamespace, credential);
    }

    public ValueTask DisposeAsync() => Client.DisposeAsync();
}
