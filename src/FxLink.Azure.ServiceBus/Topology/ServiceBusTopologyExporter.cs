using System.Text.Json;
using FxLink.Azure.ServiceBus.Registries;

namespace FxLink.Azure.ServiceBus.Topology;

/// <summary>
/// Describes the Service Bus entities FxLink expects. The emulator (and any environment where topology is created
/// outside the app, with AutoProvision(false)) can't have entities created at run time, so it needs them declared up
/// front — this renders that declaration from the registered consumers.
/// </summary>
public interface IServiceBusTopologyExporter
{
    /// <summary>
    /// The emulator's Config.json for the current consumers: topics, subscriptions with the mandatory
    /// targeting rule, and the session-enabled reply queue.
    /// </summary>
    string ToEmulatorConfigJson(string namespaceName = "sbemulatorns");
}

internal sealed class ServiceBusTopologyExporter(
    ServiceBusTopologyBuilder builder,
    IServiceBusConfiguration configuration) : IServiceBusTopologyExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string ToEmulatorConfigJson(string namespaceName = "sbemulatorns")
    {
        var topology = builder.Build();
        var lockDuration = ServiceBusTopology.ToIso(configuration.LockDuration);

        var topics = topology.Topics.Select(topic => new
        {
            Name = topic.Name,
            Properties = new
            {
                DefaultMessageTimeToLive = "P14D",
                DuplicateDetectionHistoryTimeWindow = "PT20S",
                RequiresDuplicateDetection = false
            },
            Subscriptions = topic.Subscriptions.Select(subscription => new
            {
                Name = subscription.Name,
                Properties = new
                {
                    DeadLetteringOnMessageExpiration = true,
                    DefaultMessageTimeToLive = "P14D",
                    LockDuration = lockDuration,
                    MaxDeliveryCount = configuration.MaxDeliveryCount,
                    ForwardDeadLetteredMessagesTo = "",
                    ForwardTo = "",
                    RequiresSession = false
                },
                Rules = new[]
                {
                    new
                    {
                        Name = Constants.ServiceBusConstants.TargetRuleName,
                        Properties = new
                        {
                            FilterType = "Sql",
                            SqlFilter = new { SqlExpression = ServiceBusTopology.TargetRule(subscription.Name) }
                        }
                    }
                }
            }).ToArray()
        }).ToArray();

        var replyQueue = new
        {
            Name = topology.ReplyQueueName,
            Properties = new
            {
                DeadLetteringOnMessageExpiration = false,
                DefaultMessageTimeToLive = "PT1H",
                DuplicateDetectionHistoryTimeWindow = "PT20S",
                ForwardDeadLetteredMessagesTo = "",
                ForwardTo = "",
                LockDuration = lockDuration,
                MaxDeliveryCount = configuration.MaxDeliveryCount,
                RequiresDuplicateDetection = false,
                RequiresSession = true
            }
        };

        return JsonSerializer.Serialize(new
        {
            UserConfig = new
            {
                Namespaces = new[]
                {
                    new { Name = namespaceName, Queues = new[] { replyQueue }, Topics = topics }
                },
                Logging = new { Type = "File" }
            }
        }, JsonOptions);
    }
}
