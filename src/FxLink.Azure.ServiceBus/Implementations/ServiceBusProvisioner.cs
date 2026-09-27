using Azure;
using Azure.Messaging.ServiceBus.Administration;
using FxLink.Azure.ServiceBus.Abstractions;
using FxLink.Azure.ServiceBus.Constants;
using FxLink.Azure.ServiceBus.Registries;
using FxLink.Azure.ServiceBus.Topology;

namespace FxLink.Azure.ServiceBus.Implementations;

// Creates or updates every entity in the topology. Idempotent, so it is safe to run on each start — and each
// restart the supervisor performs.
internal sealed class ServiceBusProvisioner(
    IServiceBusConnectionProvider connection,
    IServiceBusConfiguration configuration)
{
    private ServiceBusAdministrationClient Admin => connection.Administration;

    public async Task ProvisionAsync(ServiceBusTopology topology, CancellationToken token)
    {
        foreach (var topic in topology.Topics)
        {
            if (!await Admin.TopicExistsAsync(topic.Name, token))
                await Admin.CreateTopicAsync(new CreateTopicOptions(topic.Name), token);

            foreach (var subscription in topic.Subscriptions)
                await ProvisionSubscriptionAsync(topic.Name, subscription.Name, token);
        }

        await ProvisionReplyQueueAsync(topology.ReplyQueueName, token);
    }

    private async Task ProvisionSubscriptionAsync(string topic, string subscription, CancellationToken token)
    {
        var expression = ServiceBusTopology.TargetRule(subscription);

        if (!await Admin.SubscriptionExistsAsync(topic, subscription, token))
        {
            // Created together with its rule, so it never exists with the default match-all rule in place.
            var options = new CreateSubscriptionOptions(topic, subscription)
            {
                MaxDeliveryCount = configuration.MaxDeliveryCount,
                LockDuration = configuration.LockDuration,
                DeadLetteringOnMessageExpiration = true
            };
            await Admin.CreateSubscriptionAsync(options,
                new CreateRuleOptions(ServiceBusConstants.TargetRuleName, new SqlRuleFilter(expression)), token);
            return;
        }

        var existing = (await Admin.GetSubscriptionAsync(topic, subscription, token)).Value;
        existing.MaxDeliveryCount = configuration.MaxDeliveryCount;
        existing.LockDuration = configuration.LockDuration;
        existing.DeadLetteringOnMessageExpiration = true;
        await Admin.UpdateSubscriptionAsync(existing, token);

        // The rule is mandatory: drop the default match-all rule and make sure ours is the one in place.
        var hasTargetRule = false;
        await foreach (var rule in Admin.GetRulesAsync(topic, subscription, token))
        {
            if (rule.Name == ServiceBusConstants.TargetRuleName)
            {
                hasTargetRule = true;
                rule.Filter = new SqlRuleFilter(expression);
                await Admin.UpdateRuleAsync(topic, subscription, rule, token);
            }
            else
            {
                await Admin.DeleteRuleAsync(topic, subscription, rule.Name, token);
            }
        }

        if (!hasTargetRule)
            await Admin.CreateRuleAsync(topic, subscription,
                new CreateRuleOptions(ServiceBusConstants.TargetRuleName, new SqlRuleFilter(expression)), token);
    }

    private async Task ProvisionReplyQueueAsync(string queueName, CancellationToken token)
    {
        if (!await Admin.QueueExistsAsync(queueName, token))
        {
            await Admin.CreateQueueAsync(new CreateQueueOptions(queueName)
            {
                RequiresSession = true,
                LockDuration = configuration.LockDuration,
                // Replies are only useful while the requester is still waiting.
                DefaultMessageTimeToLive = TimeSpan.FromHours(1)
            }, token);
            return;
        }

        var existing = (await Admin.GetQueueAsync(queueName, token)).Value;
        if (!existing.RequiresSession)
            throw new InvalidOperationException(
                $"Reply queue '{queueName}' exists without sessions enabled; request/response needs a " +
                "session-enabled queue. Recreate it with sessions, or choose another ReplyQueueName(...).");
    }
}
