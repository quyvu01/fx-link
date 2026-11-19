using System.Reflection;
using FxLink.Registries;
using FxLink.Registries.Implementations;

namespace FxLink.Extensions;

public static class StateMachineActivityExtensions
{
    extension(IStateMachineConfigurator configurator)
    {
        public IStateMachineConfigurator AddActivitiesFromAssembly<TAssembly>() =>
            configurator.AddActivitiesFromAssemblies(typeof(TAssembly).Assembly);

        public IStateMachineConfigurator AddActivitiesFromAssemblies(params Assembly[] assemblies)
        {
            assemblies.ForEach(a => ((StateMachineConfigurator)configurator).AddActivitiesFromAssembly(a));
            return configurator;
        }
    }
}
