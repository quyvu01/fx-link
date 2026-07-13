using System.Reflection;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.Implementations;
using FxLink.InMemory;
using FxLink.Supervision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Registries;

internal class Configurator(IServiceCollection services) : IConfigurator
{
    public IServiceCollection Services { get; } = services;
    public IMessageKeys MessageKeys { get; } = new MessageKeys();
    internal ISupervisorOptions SupervisorOptions { get; private set; } = new SupervisorOptions();

    public void AddConsumer<TConsumer>()
        where TConsumer : IConsumer => AddConsumer(typeof(TConsumer));

    public void AddConsumerDefinition<TConsumerDefinition>() where TConsumerDefinition : IConsumerDefinition =>
        AddConsumerTypeDefinition(typeof(TConsumerDefinition));

    public void AddConsumersFromAssemblies(Assembly assembly)
    {
        var consumerTypes = assembly.DefinedTypes
            .Where(type => typeof(IConsumer).IsAssignableFrom(type) && type.IsClosedConcreteType());
        foreach (var consumerType in consumerTypes) AddConsumer(consumerType);
    }

    // Not use in production
    public void UseInMemory()
    {
        Services.TryAddSingleton(typeof(IInMemoryMessageProcessor<>), typeof(InMemoryInMemoryMessage<>));
        Services.AddSingleton<InMemoryMessageUnPublisherDispatcher>();
        Services.TryAddSingleton(typeof(IClientConnector<>), typeof(InMemoryClientConnector<>));
        Services.AddSingleton<InMemoryResponseProcessor>();
    }

    public void ConfigureSupervisor(Action<ISupervisorOptions> options)
    {
        var supervisorOptions = new SupervisorOptions();
        options?.Invoke(supervisorOptions);
        SupervisorOptions = supervisorOptions;
    }


    // Need to check if we have edge cases here!
    public void AddConsumer(Type consumerType)
    {
        consumerType
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

    internal void AddConsumerTypeDefinition(Type consumerDefinition)
    {
        if (consumerDefinition.GetGenericBaseType(typeof(AbstractConsumerDefinition<>)) is { } configForConsumer)
        {
            var serviceType = typeof(IAbstractConsumerDefinition<>)
                .MakeGenericType(configForConsumer.GetGenericArguments());
            Services.TryAddEnumerable(new ServiceDescriptor(serviceType, consumerDefinition,
                ServiceLifetime.Singleton));
            return;
        }

        if (consumerDefinition.GetGenericBaseType(typeof(AbstractConsumerDefinition<,>)) is { } configForMessage)
        {
            var serviceType = typeof(IAbstractConsumerDefinition<,>)
                .MakeGenericType(configForMessage.GetGenericArguments());
            Services.TryAddEnumerable(new ServiceDescriptor(serviceType, consumerDefinition,
                ServiceLifetime.Singleton));
        }
    }
}