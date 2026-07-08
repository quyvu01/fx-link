using System.Reflection;
using FxLink.Abstractions;
using FxLink.Supervision;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IConfigurator
{
    IServiceCollection Services { get; }
    IMessageKeys MessageKeys { get; }
    void AddConsumer<TConsumer>() where TConsumer : IConsumer;
    void AddConsumersFromAssemblies(Assembly assembly);
    void AddConsumersFromAssemblies(params Assembly[] assemblies);
    void UseInMemory();
    public void ConfigureSupervisor(Action<ISupervisorOptions> options);
}