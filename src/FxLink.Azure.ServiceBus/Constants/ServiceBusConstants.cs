namespace FxLink.Azure.ServiceBus.Constants;

internal static class ServiceBusConstants
{
    public const string DefaultReplyQueueName = "fxlink-replies";
    public const int DefaultMaxDeliveryCount = 5;
    public const int DefaultMaxConcurrentCalls = 8;
    public const int DefaultPrefetchCount = 0;
    public static readonly TimeSpan DefaultLockDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultMaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5);

    // Service Bus limits: a lock lasts 5 seconds to 5 minutes; names are capped per entity kind.
    public static readonly TimeSpan MinLockDuration = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxLockDuration = TimeSpan.FromMinutes(5);
    public const int MaxTopicNameLength = 260;
    public const int MaxSubscriptionNameLength = 50;

    // Application properties on a message.
    public const string MessageTypeProperty = "MessageType";

    // Set on a retry so only the subscription that failed receives it — see ServiceBusTopology.TargetRule.
    public const string TargetProperty = "x-target";

    // Context header carrying the requester's session id, next to the core ReplyToKey (the queue name).
    public const string ReplyToSessionKey = "x-reply-to-session";

    public const string TargetRuleName = "fxlink-target";

    // Unrecoverable processor errors (entity gone, access revoked) in a row before the connector faults
    // and the supervisor restarts it, re-provisioning the topology.
    public const int FatalErrorThreshold = 3;
}
