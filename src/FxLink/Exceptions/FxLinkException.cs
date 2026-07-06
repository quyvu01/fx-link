using FxLink.Abstractions;

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
}
