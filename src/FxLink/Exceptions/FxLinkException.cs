using FxLink.Abstractions;
using FxLink.Registries;

namespace FxLink.Exceptions;

/// <summary>
/// Groups the exceptions thrown by the FxLink core (pipeline behavior registration, request/response).
/// </summary>
public static class FxLinkException
{
    /// <summary>A pipeline behavior registered for consumers does not implement IConsumerPipelineBehavior&lt;&gt;.</summary>
    public sealed class ConsumerPipelineBehaviorTypeMismatch(Type type) :
        DistributedException($"{type.Name} must implement {typeof(IConsumerPipelineBehavior<>).FullName}!");

    /// <summary>A pipeline behavior registered for publishers does not implement IPublisherPipelineBehavior&lt;&gt;.</summary>
    public sealed class PublisherPipelineBehaviorTypeMismatch(Type type) :
        DistributedException($"{type.Name} must implement {typeof(IPublisherPipelineBehavior<>).FullName}!");

    /// <summary>A request/reply call was configured with a negative timeout.</summary>
    public sealed class RequestTimeoutMustNotBeNegative(TimeSpan timeSpan)
        : DistributedException($"Request timeout: {timeSpan} must not be negative!");

    /// <summary>UseMessageRetry was called on a non-generic IConsumerConfigurator.</summary>
    public sealed class ConsumerConfiguratorMustBeGeneric(Type configuratorType) :
        DistributedException($"{configuratorType.Name} must be a generic type implementing {typeof(IConsumerConfigurator<>).FullName}!");

    /// <summary>The consumer type does not implement IConsumer&lt;&gt; for the given message type.</summary>
    public sealed class ConsumerMessageTypeMismatch(Type consumerType, Type messageType) :
        DistributedException($"{consumerType.Name} must implement {typeof(IConsumer<>).FullName} for message type {messageType.Name}!");
}
