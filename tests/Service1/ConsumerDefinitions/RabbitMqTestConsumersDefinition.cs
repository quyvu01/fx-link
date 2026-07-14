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
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4));
            c.Ignore<TimeoutException>().Ignore<InvalidOperationException>();
        });
    }
}

public sealed class RabbitMqTestConsumersDefinitionForMessage :
    AbstractConsumerDefinition<RabbitMqTestConsumerWithDefinition,
        RabbitMqTestPublisher>
{
    public override void Configure(IConsumerDefinition<RabbitMqTestConsumerWithDefinition,
        RabbitMqTestPublisher> consumerDefinition)
    {
        consumerDefinition.UseMessageRetry(c =>
        {
            c.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
        });
    }
}