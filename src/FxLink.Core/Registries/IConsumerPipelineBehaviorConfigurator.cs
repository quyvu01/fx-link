using FxLink.Core.Abstractions;
using FxLink.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.Registries;

public interface IConsumerPipelineBehaviorConfigurator
{
    IConsumerPipelineBehaviorConfigurator Of<TPipelineBehavior>
        (ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where TPipelineBehavior : IConsumerPipelineBehavior;

    IConsumerPipelineBehaviorConfigurator Of(Type runtimePipelineType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped);
}

public sealed class ConsumerPipelineBehaviorConfigurator(IServiceCollection services)
    : IConsumerPipelineBehaviorConfigurator
{
    private static readonly Type ServiceType = typeof(IConsumerPipelineBehavior<>);

    public IConsumerPipelineBehaviorConfigurator Of<TPipelineBehavior>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TPipelineBehavior : IConsumerPipelineBehavior
        => Of(typeof(TPipelineBehavior), serviceLifetime);

    // Todo: Need to check more edge case, especial Generic type
    public IConsumerPipelineBehaviorConfigurator Of(Type runtimePipelineType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        var signatureInterfaceTypes = runtimePipelineType.GetInterfaces()
            .Where(a => a.IsGenericType && a.GetGenericTypeDefinition() == ServiceType)
            .ToList();

        if (signatureInterfaceTypes is not { Count: > 0 })
            throw new DistributedException.TypeIsNotConsumerPipelineBehavior(runtimePipelineType);

        if (runtimePipelineType.IsGenericType && runtimePipelineType.ContainsGenericParameters)
        {
            var serviceDescriptor = new ServiceDescriptor(ServiceType, runtimePipelineType, serviceLifetime);
            services.Add(serviceDescriptor);
            return this;
        }

        signatureInterfaceTypes.ForEach(serviceType =>
        {
            var serviceDescriptor = new ServiceDescriptor(serviceType, runtimePipelineType, serviceLifetime);
            services.Add(serviceDescriptor);
        });
        return this;
    }
}