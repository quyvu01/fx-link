using FxLink.Core.Abstractions;

namespace FxLink.Core.Exceptions;

/// <summary>
/// Contains all custom exception types used by the FxMap framework.
/// </summary>
/// <remarks>
/// These exceptions provide detailed error messages for common configuration
/// and runtime issues encountered when using the FxMap framework.
/// </remarks>
public static class DistributedException
{
    public sealed class TypeIsNotConsumerPipelineBehavior(Type type) :
        Exception($"{type.Name} must implement {typeof(IConsumerPipelineBehavior<>).FullName}!");

    public sealed class TypeIsNotPublisherPipelineBehavior(Type type) :
        Exception($"{type.Name} must implement {typeof(IPublisherPipelineBehavior<>).FullName}!");
}