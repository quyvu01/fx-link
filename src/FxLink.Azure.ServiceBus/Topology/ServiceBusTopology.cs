using System.Xml;
using FxLink.Azure.ServiceBus.Constants;

namespace FxLink.Azure.ServiceBus.Topology;

internal sealed record SubscriptionModel(string Name, Type ConsumerType);

internal sealed record TopicModel(string Name, Type MessageType, IReadOnlyList<SubscriptionModel> Subscriptions);

// The single description of the entities FxLink expects, built from the registered consumers. Both
// AutoProvision and the emulator Config.json export read it, so they can't drift apart.
internal sealed record ServiceBusTopology(IReadOnlyList<TopicModel> Topics, string ReplyQueueName)
{
    // Replaces the default match-all rule: ordinary messages (no x-target) reach every subscription, a
    // retry (x-target = subscription name) reaches only the subscription that failed.
    internal static string TargetRule(string subscriptionName) =>
        $"[{ServiceBusConstants.TargetProperty}] IS NULL OR [{ServiceBusConstants.TargetProperty}] = " +
        $"'{subscriptionName.Replace("'", "''")}'";

    internal static string ToIso(TimeSpan value) => XmlConvert.ToString(value);
}
