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

    /// <summary>A dynamic message contract type passed to the proxy builder was not an interface.</summary>
    public sealed class MessageContractMustBeInterface(Type type) :
        DistributedException($"{type.Name} must be an interface to be used as a dynamic message contract!");

    /// <summary>A dynamic message contract interface declares a member other than a property.</summary>
    public sealed class MessageContractMustOnlyDeclareProperties(Type type) :
        DistributedException(
            $"{type.Name} must only declare properties (get/set) — methods are not supported on dynamic message contracts!");

    /// <summary>A concrete message type passed to PublishAsync&lt;TMessage&gt;(object values) has no public parameterless constructor.</summary>
    public sealed class MessageContractRequiresParameterlessConstructor(Type type) :
        DistributedException(
            $"{type.Name} must have a public parameterless constructor to be hydrated from PublishAsync<{type.Name}>(object values)!");

    /// <summary>RequestAsync(object values) was called on an IRequester whose concrete type isn't generic over TMessage, so TMessage can't be resolved via reflection.</summary>
    public sealed class RequesterMustBeGeneric(Type requesterType) :
        DistributedException($"{requesterType.Name} must be a generic type implementing {typeof(IRequester<>).FullName}!");

    /// <summary>GetPayload&lt;T&gt; was called on a context but no payload of that type was ever set via SetPayload&lt;T&gt;.</summary>
    public sealed class ContextPayloadNotFound(Type payloadType) :
        DistributedException($"No payload of type {payloadType.Name} has been set on this context. Call SetPayload<{payloadType.Name}>() before GetPayload<{payloadType.Name}>().");
}
