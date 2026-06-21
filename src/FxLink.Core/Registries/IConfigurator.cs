using System.Reflection;
using FxLink.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.Registries;

public interface IConfigurator
{
    IServiceCollection Services { get; }
    void AddConsumer<TConsumer>() where TConsumer : IConsumer;
    void AddConsumersFromAssemblies(Assembly assembly);
    void AddConsumersFromAssemblies(params Assembly[] assemblies);
    void UseInMemory();
}