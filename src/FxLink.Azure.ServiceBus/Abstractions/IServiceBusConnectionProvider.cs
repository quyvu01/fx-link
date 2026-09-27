using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace FxLink.Azure.ServiceBus.Abstractions;

/// <summary>
/// Owns the single ServiceBusClient (and the administration client used for provisioning) shared by every
/// Service Bus transport component in this process. The client pools its AMQP connection and is thread-safe.
/// </summary>
internal interface IServiceBusConnectionProvider
{
    ServiceBusClient Client { get; }
    ServiceBusAdministrationClient Administration { get; }
}
