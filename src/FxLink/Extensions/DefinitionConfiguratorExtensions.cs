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
                .Where(type => typeof(IConsumerDefinition)
                    .IsAssignableFrom(type) && type.IsClosedConcreteType());
            consumerDefinitions.ForEach(consumerType =>
                ((Configurator)configuration).AddConsumerTypeDefinition(consumerType));
        }

        public void AddConsumerDefinitionsFromAssemblies(params Assembly[] assemblies)
            => assemblies.ForEach(configuration.AddConsumerDefinitionsFromAssemblies);

        public void AddMessageDefinitionsFromAssemblies(Assembly assembly)
        {
            var messageTypeDefinitions = assembly.DefinedTypes
                .Where(type => typeof(IMessageDefinition)
                    .IsAssignableFrom(type) && type.IsClosedConcreteType());
            messageTypeDefinitions.ForEach(messageTypeDefinition =>
                ((Configurator)configuration).AddMessageTypeDefinition(messageTypeDefinition));
        }

        public void AddMessageDefinitionsFromAssemblies(params Assembly[] assembly)
            => assembly.ForEach(configuration.AddMessageDefinitionsFromAssemblies);
    }
}