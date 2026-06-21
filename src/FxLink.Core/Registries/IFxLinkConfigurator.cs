using System.Reflection;
using FxLink.Core.Abstractions;

namespace FxLink.Core.Registries;

public interface IFxLinkConfigurator
{
    void AddConsumer<TConsumer>() where TConsumer : IConsumer;
    void AddConsumersFromAssemblies(Assembly assembly);
    void AddConsumersFromAssemblies(params Assembly[] assemblies);
    void UseInMemory();
}