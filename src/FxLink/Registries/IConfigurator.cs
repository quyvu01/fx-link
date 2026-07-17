using FxLink.Abstractions;
using FxLink.Supervision;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IConfigurator
{
    IServiceCollection Services { get; }
    IMessageKeys MessageKeys { get; }
    void AddConsumer<TConsumer>() where TConsumer : IConsumer;
    void AddConsumerDefinition<TConsumerDefinition>() where TConsumerDefinition : IConsumerConfigurator;
    void UseInMemory();
    void ConfigureSupervisor(Action<ISupervisorOptions> options);
}