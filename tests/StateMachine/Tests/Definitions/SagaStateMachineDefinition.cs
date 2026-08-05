using FxLink.Abstractions;
using FxLink.Registries;
using StateMachine.Tests.Consumers;

namespace StateMachine.Tests.Definitions;

public class SagaStateMachineDefinition : ConsumerDefinition<GetNameConsumer>
{
    public override void Configure(IConsumerConfigurator<GetNameConsumer> options)
    {
        options.UseMessageRetry(c =>
        {
            c.Intervals(TimeSpan.FromSeconds(1));
            c.Ignore<Exception>();
        });
    }
}