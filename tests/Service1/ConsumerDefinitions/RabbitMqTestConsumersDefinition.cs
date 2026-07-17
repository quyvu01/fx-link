using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.Registries;
using Service1.Consumers.RabbitMqTests;
using Service1.RabbitMqTests;

namespace Service1.ConsumerDefinitions;

public sealed class RabbitMqTestConsumersDefinition : ConsumerDefinition<RabbitMqTestConsumerWithDefinition>
{
    public override void Configure(IConsumerConfigurator<RabbitMqTestConsumerWithDefinition> options)
    {
        options.UseMessageRetry(c =>
        {
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
            c.Ignore<TimeoutException>();
        });
        options.UseMessageRetry<RabbitMqTestRetry>(c =>
        {
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
            c.Ignore<InvalidDataException>();
        });
    }
}