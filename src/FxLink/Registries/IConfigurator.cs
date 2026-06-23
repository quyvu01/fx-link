using System.Reflection;
using FxLink.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IConfigurator
{
    IServiceCollection Services { get; }
    void AddConsumer<TConsumer>() where TConsumer : IConsumer;
    void AddConsumersFromAssemblies(Assembly assembly);
    void AddConsumersFromAssemblies(params Assembly[] assemblies);
    void UseInMemory();
}