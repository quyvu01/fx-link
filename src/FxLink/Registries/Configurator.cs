using System.Reflection;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.Implementations;
using FxLink.InMemory;
using FxLink.Supervision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Registries;

internal class Configurator(IServiceCollection serviceCollection) : IConfigurator
{
    public IServiceCollection Services { get; } = serviceCollection;
    public IMessageKeys MessageKeys { get; } = new MessageKeys();
    internal ISupervisorOptions SupervisorOptions { get; private set; } = new SupervisorOptions();

    public void AddConsumer<TConsumer>(Action<IConsumerDefinition<TConsumer>> options = null)
        where TConsumer : IConsumer
    {
        var consumerDefinition = new ConsumerDefinition<TConsumer>();
        options?.Invoke(consumerDefinition);
        AddConsumer(typeof(TConsumer), consumerDefinition);
    }

    public void AddConsumersFromAssemblies(Assembly assembly)
    {
        var consumerTypes = assembly.DefinedTypes
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
    private void AddConsumer(Type consumerType, IConsumerDefinition consumerDefinition = null)
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
}