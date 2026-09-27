using FxLink.Abstractions;
using FxLink.Azure.ServiceBus.Extensions;
using FxLink.Azure.ServiceBus.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Azure.ServiceBus.Topology;

internal sealed class ServiceBusTopologyBuilder(
    IMessageKeys messageKeys,
    IServiceProvider serviceProvider,
    IServiceBusConfiguration configuration)
{
    public ServiceBusTopology Build()
    {
        var messageKeyMap = messageKeys.GetMessageKeys(); // messageType -> consumerTypes[]
        var topics = messageKeyMap
            .Select(kv => new TopicModel(ResolveTopicName(kv.Key), kv.Key,
                kv.Value.Select(consumerType => new SubscriptionModel(ResolveSubscriptionName(consumerType),
                    consumerType)).ToArray()))
            .ToArray();
        return new ServiceBusTopology(topics, configuration.ReplyQueueName);
    }

    // A message type can override its topic via IMessageDefinition<TMessage>'s MessageConfigurator.Name(...); a
    // definition registered without a name (e.g. one that only calls UseRawJsonSerializer()) falls back to the
    // type-based default.
    public string ResolveTopicName(Type messageType)
    {
        var messageDefinition = serviceProvider.GetService(typeof(IMessageDefinition<>)
            .MakeGenericType(messageType)) as IMessageDefinition;
        var customName = messageDefinition?.MessageConfigurator.GetName();
        return customName is { Length: > 0 }
            ? ServiceBusNamingExtensions.SanitizeTopic(customName)
            : messageType.GetTopicName();
    }

    // A consumer can override its subscription name via IConsumerConfigurator<TConsumer>.ReceivedEndpoint(...).
    public string ResolveSubscriptionName(Type consumerType)
    {
        var consumerConfiguration = serviceProvider
                .GetRequiredService(typeof(IConsumerConfiguratorResolver<>).MakeGenericType(consumerType)) as
            IConsumerConfiguratorResolver;
        var customName = consumerConfiguration!.Resolve<IServiceBusReceiveEndpointDefinition>()?.ReceiveEndpoint;
        return customName is { Length: > 0 }
            ? ServiceBusNamingExtensions.SanitizeSubscription(customName)
            : consumerType.GetSubscriptionName();
    }
}
