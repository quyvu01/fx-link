using FxLink.Abstractions;

namespace FxLink.Exceptions;

public static class DistributedException
{
    public sealed class TypeIsNotConsumerPipelineBehavior(Type type) :
        Exception($"{type.Name} must implement {typeof(IConsumerPipelineBehavior<>).FullName}!");

    public sealed class TypeIsNotPublisherPipelineBehavior(Type type) :
        Exception($"{type.Name} must implement {typeof(IPublisherPipelineBehavior<>).FullName}!");
}