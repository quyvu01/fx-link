using System.Reflection;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.Implementations;
using FxLink.InMemory;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Registries;

public class Configurator(IServiceCollection serviceCollection) : IConfigurator
{
    public IServiceCollection Services { get; } = serviceCollection;
    public IMessageKeys MessageKeys { get; } = new MessageKeys();
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
        Services.TryAddSingleton(typeof(IMessageProcessor<>), typeof(InMemoryMessage<>));
    }

    // Need to check if we have edge cases here!
    private void AddConsumer(Type consumerType) => consumerType
        .GetInterfaces()
        .Where(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IConsumer<>))
        .ForEach(serviceType =>
        {
            var messageType = serviceType.GetGenericArguments()[0];
            Services.TryAddEnumerable(new ServiceDescriptor(serviceType: serviceType, serviceKey: consumerType,
                implementationType: consumerType, ServiceLifetime.Scoped));
            MessageKeys.AddMessageKey(messageType, consumerType);
        });
}