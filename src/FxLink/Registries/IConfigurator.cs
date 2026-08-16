using FxLink.Abstractions;
using FxLink.Supervision;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Registries;

public interface IConfigurator
{
    IServiceCollection Services { get; }
    void AddConsumer<TConsumer>() where TConsumer : IConsumer;
    void AddConsumerDefinition<TConsumerDefinition>() where TConsumerDefinition : IConsumerDefinition;
    void AddMessageDefinition<TMessageDefinition>() where TMessageDefinition : IMessageDefinition;
    void UseInMemory();
    void ConfigureSupervisor(Action<ISupervisorOptions> options);
}