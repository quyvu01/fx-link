using FxLink.Abstractions;
using FxLink.Registries;
using Order.Dtos;

namespace Order.Definitions;

public sealed class NoRefMessageDefinition : MessageDefinition<INoRefMessage>
{
    public override void Configure(IMessageConfigurator<INoRefMessage> options)
    {
        options.Name("some-test-message");
    }
}