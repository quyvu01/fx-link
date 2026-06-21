using System.Reflection;
using FxLink.Core.Abstractions;
using FxLink.Core.Extensions;
using FxLink.Core.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Core.Registries;

public class FxLinkConfigurator(IServiceCollection serviceCollection) : IFxLinkConfigurator
{
    public void AddConsumer<TConsumer>() where TConsumer : IConsumer => AddConsumer(typeof(TConsumer));

    public void AddConsumersFromAssemblies(Assembly assembly)
    {
        var consumerTypes = assembly.ExportedTypes
            .Where(type => typeof(IConsumer).IsAssignableFrom(type) && type.IsClosedConcreteType());
        foreach (var consumerType in consumerTypes) AddConsumer(consumerType);
    }


    public void AddConsumersFromAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies) AddConsumersFromAssemblies(assembly);
    }

    // Not use in production
    public void UseInMemory()
    {
        serviceCollection.AddSingleton(typeof(IServer<>), typeof(InMemoryBus<>));
        serviceCollection.AddSingleton(typeof(IClient<>), typeof(InMemoryBus<>));
    }

    // Need to check if we have edge cases here!
    private void AddConsumer(Type consumerType) => consumerType
        .GetInterfaces()
        .Where(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IConsumer<>))
        .ForEach(serviceType => serviceCollection
            .TryAddEnumerable(new ServiceDescriptor(serviceType, consumerType, ServiceLifetime.Scoped)));
}