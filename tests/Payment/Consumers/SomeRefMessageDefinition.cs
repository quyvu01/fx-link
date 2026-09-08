using FxLink.Abstractions;
using FxLink.Registries;

namespace Payment.Consumers;

public sealed class SomeRefMessageDefinition : MessageDefinition<ISomeRefMessage>
{
    public override void Configure(IMessageConfigurator<ISomeRefMessage> options)
    {
        options.Name("some-test-message");
    }
}