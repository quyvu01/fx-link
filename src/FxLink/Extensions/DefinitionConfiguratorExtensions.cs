using System.Reflection;
using FxLink.Abstractions;
using FxLink.Registries;

namespace FxLink.Extensions;

public static class DefinitionConfiguratorExtensions
{
    extension(IConfigurator configuration)
    {
        public void AddConsumerDefinitionsFromAssemblies(Assembly assembly)
        {
            var consumerDefinitions = assembly.DefinedTypes
                .Where(type => typeof(IAbstractConsumerDefinition)
                    .IsAssignableFrom(type) && type.IsClosedConcreteType());
            foreach (var consumerType in consumerDefinitions)
                ((Configurator)configuration).AddConsumerTypeDefinition(consumerType);
        }

        public void AddConsumerDefinitionsFromAssemblies(params Assembly[] assemblies)
            => assemblies.ForEach(configuration.AddConsumerDefinitionsFromAssemblies);
    }
}