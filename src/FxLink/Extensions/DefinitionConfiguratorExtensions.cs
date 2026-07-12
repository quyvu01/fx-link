using System.Reflection;
using FxLink.Registries;

namespace FxLink.Extensions;

public static class DefinitionConfiguratorExtensions
{
    extension(IConfigurator configuration)
    {
        public void AddConsumerDefinition<TConsumerDefinition>() where TConsumerDefinition : IConsumerDefinition
        {
        }

        public void AddConsumerDefinitionsFromAssemblies(Assembly assembly)
        {
        }

        public void AddConsumerDefinitionsFromAssemblies(params Assembly[] assemblies)
            => assemblies.ForEach(a => AddConsumerDefinitionsFromAssemblies(configuration, a));
    }
}