using System.Reflection;
using FxLink.Abstractions;
using FxLink.Registries;

namespace FxLink.Extensions;

public static class ConsumerRegistrationExtensions
{
    extension(IConfigurator configurator)
    {
        public void AddConsumersFromAssemblies(Assembly assembly)
        {
            var consumerTypes = assembly.DefinedTypes
                .Where(type => typeof(IConsumer).IsAssignableFrom(type) && type.IsClosedConcreteType());
            foreach (var consumerType in consumerTypes) ((Configurator)configurator)?.AddConsumer(consumerType);
        }

        public void AddConsumersFromAssemblies(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies) configurator.AddConsumersFromAssemblies(assembly);
        }
    }
}