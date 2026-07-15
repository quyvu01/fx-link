using FxLink.Registries;
using Service1.Consumers.RabbitMqTests;
using Service1.RabbitMqTests;

namespace Service1.ConsumerDefinitions;

public sealed class RabbitMqTestConsumersDefinition : AbstractConsumerDefinition<RabbitMqTestConsumerWithDefinition>
{
    public override void Configure(IConsumerDefinition<RabbitMqTestConsumerWithDefinition> consumerDefinition)
    {
        consumerDefinition.UseMessageRetry(c =>
        {
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
            c.Ignore<TimeoutException>();
        });
    }
}

public sealed class RabbitMqTestConsumersDefinitionForMessage :
    AbstractConsumerDefinition<RabbitMqTestConsumerWithDefinition, RabbitMqTestRetry>
{
    public override void Configure(IConsumerDefinition<RabbitMqTestConsumerWithDefinition,
        RabbitMqTestRetry> consumerDefinition)
    {
        consumerDefinition.UseMessageRetry(c =>
        {
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        });
    }
}