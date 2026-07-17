using FxLink.Abstractions;
using FxLink.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IPublisherPipelineBehaviorConfigurator
{
    IPublisherPipelineBehaviorConfigurator Of<TPipelineBehavior>
        (ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where TPipelineBehavior : IPublisherPipelineBehavior;

    IPublisherPipelineBehaviorConfigurator Of(Type runtimePipelineType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped);
}

public sealed class PublisherPipelineBehaviorConfigurator(IServiceCollection services)
    : IPublisherPipelineBehaviorConfigurator
{
    private static readonly Type ServiceType = typeof(IPublisherPipelineBehavior<>);

    public IPublisherPipelineBehaviorConfigurator Of<TPipelineBehavior>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        where TPipelineBehavior : IPublisherPipelineBehavior
        => Of(typeof(TPipelineBehavior), serviceLifetime);

    public IPublisherPipelineBehaviorConfigurator Of(Type runtimePipelineType,
        ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        var signatureInterfaceTypes = runtimePipelineType.GetInterfaces()
            .Where(a => a.IsGenericType && a.GetGenericTypeDefinition() == ServiceType)
            .ToList();

        if (signatureInterfaceTypes is not { Count: > 0 })
            throw new FxLinkException.PublisherPipelineBehaviorTypeMismatch(runtimePipelineType);

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