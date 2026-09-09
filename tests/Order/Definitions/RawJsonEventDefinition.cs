using FxLink.Abstractions;
using FxLink.Registries;
using Order.Dtos;

namespace Order.Definitions;

public sealed class RawJsonEventDefinition : MessageDefinition<IRawJsonEvent>
{
    public override void Configure(IMessageConfigurator<IRawJsonEvent> options)
    {
        options.UseRawJsonSerializer();
        options.Name("raw-json-message");
    }
}