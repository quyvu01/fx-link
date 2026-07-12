using System.Reflection;
using FxLink.Abstractions;
using FxLink.Supervision;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IConfigurator
{
    IServiceCollection Services { get; }
    IMessageKeys MessageKeys { get; }
    void AddConsumer<TConsumer>(Action<IConsumerDefinition<TConsumer>> options = null) where TConsumer : IConsumer;
    void AddConsumersFromAssemblies(Assembly assembly);
    void AddConsumersFromAssemblies(params Assembly[] assemblies);
    void UseInMemory();
    void ConfigureSupervisor(Action<ISupervisorOptions> options);
}